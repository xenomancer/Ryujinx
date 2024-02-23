using ARMeilleure.Memory;
using Ryujinx.Cpu.Signal;
using Ryujinx.Memory;
using Ryujinx.Memory.Range;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Cpu.Jit
{
    /// <summary>
    /// Represents a CPU memory manager which maps guest virtual memory directly onto a host virtual region.
    /// </summary>
    public sealed class MemoryManagerHostTracked : VirtualMemoryManagerRefCountedBase<ulong, ulong>, IWritableBlock, IMemoryManager, IVirtualMemoryManagerTracked
    {
        private readonly InvalidAccessHandler _invalidAccessHandler;
        private readonly bool _unsafeMode;

        private readonly MemoryBlock _backingMemory;

        public int AddressSpaceBits { get; }

        public MemoryTracking Tracking { get; }

        private readonly NativePageTable _nativePageTable;
        private readonly AddressSpacePartitioned _addressSpace;

        private readonly ManagedPageFlags _pages;

        protected override ulong AddressSpaceSize { get; }

        /// <inheritdoc/>
        public bool Supports4KBPages => false;

        public IntPtr PageTablePointer => _nativePageTable.PageTablePointer;

        public MemoryManagerType Type => _unsafeMode ? MemoryManagerType.HostTrackedUnsafe : MemoryManagerType.HostTracked;

        public event Action<ulong, ulong> UnmapEvent;

        /// <summary>
        /// Creates a new instance of the host tracked memory manager.
        /// </summary>
        /// <param name="backingMemory">Physical backing memory where virtual memory will be mapped to</param>
        /// <param name="addressSpaceSize">Size of the address space</param>
        /// <param name="unsafeMode">True if unmanaged access should not be masked (unsafe), false otherwise.</param>
        /// <param name="invalidAccessHandler">Optional function to handle invalid memory accesses</param>
        public MemoryManagerHostTracked(MemoryBlock backingMemory, ulong addressSpaceSize, bool unsafeMode, InvalidAccessHandler invalidAccessHandler)
        {
            bool useProtectionMirrors = MemoryBlock.GetPageSize() > PageSize;

            Tracking = new MemoryTracking(this, PageSize, invalidAccessHandler, useProtectionMirrors);

            _backingMemory = backingMemory;
            _invalidAccessHandler = invalidAccessHandler;
            _unsafeMode = unsafeMode;
            AddressSpaceSize = addressSpaceSize;

            ulong asSize = PageSize;
            int asBits = PageBits;

            while (asSize < AddressSpaceSize)
            {
                asSize <<= 1;
                asBits++;
            }

            AddressSpaceBits = asBits;

            if (useProtectionMirrors && !NativeSignalHandler.SupportsFaultAddressPatching())
            {
                // Currently we require being able to change the fault address to something else
                // in order to "emulate" 4KB granularity protection on systems with larger page size.

                throw new PlatformNotSupportedException();
            }

            _pages = new ManagedPageFlags(asBits);
            _nativePageTable = new(asSize);
            _addressSpace = new(Tracking, backingMemory, _nativePageTable, useProtectionMirrors);
        }

        /// <inheritdoc/>
        public void Map(ulong va, ulong pa, ulong size, MemoryMapFlags flags)
        {
            AssertValidAddressAndSize(va, size);

            if (flags.HasFlag(MemoryMapFlags.Private))
            {
                _addressSpace.Map(va, pa, size);
            }

            _pages.AddMapping(va, size);
            _nativePageTable.Map(va, pa, size, _addressSpace, _backingMemory, flags.HasFlag(MemoryMapFlags.Private));

            Tracking.Map(va, size);
        }

        /// <inheritdoc/>
        public void MapForeign(ulong va, nuint hostPointer, ulong size)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void Unmap(ulong va, ulong size)
        {
            AssertValidAddressAndSize(va, size);

            _addressSpace.Unmap(va, size);

            UnmapEvent?.Invoke(va, size);
            Tracking.Unmap(va, size);

            _pages.RemoveMapping(va, size);
            _nativePageTable.Unmap(va, size);
        }

        public T Read<T>(ulong va) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetSpan(va, Unsafe.SizeOf<T>()))[0];
        }

        public T ReadTracked<T>(ulong va) where T : unmanaged
        {
            try
            {
                SignalMemoryTracking(va, (ulong)Unsafe.SizeOf<T>(), false);

                return Read<T>(va);
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }

                return default;
            }
        }

        public override void Read(ulong va, Span<byte> data)
        {
            ReadImpl(va, data);
        }

        public void Write<T>(ulong va, T value) where T : unmanaged
        {
            Write(va, MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        public void Write(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            SignalMemoryTracking(va, (ulong)data.Length, true);

            WriteImpl(va, data);
        }

        public void WriteUntracked(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            WriteImpl(va, data);
        }

        public bool WriteWithRedundancyCheck(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return false;
            }

            SignalMemoryTracking(va, (ulong)data.Length, false);

            if (TryGetVirtualContiguous(va, data.Length, out MemoryBlock memoryBlock, out ulong offset))
            {
                var target = memoryBlock.GetSpan(offset, data.Length);

                bool changed = !data.SequenceEqual(target);

                if (changed)
                {
                    data.CopyTo(target);
                }

                return changed;
            }
            else
            {
                WriteImpl(va, data);

                return true;
            }
        }

        private void WriteImpl(ulong va, ReadOnlySpan<byte> data)
        {
            try
            {
                AssertValidAddressAndSize(va, (ulong)data.Length);

                ulong endVa = va + (ulong)data.Length;
                int offset = 0;

                while (va < endVa)
                {
                    (MemoryBlock memory, ulong rangeOffset, ulong copySize) = GetMemoryOffsetAndSize(va, (ulong)(data.Length - offset));

                    data.Slice(offset, (int)copySize).CopyTo(memory.GetSpan(rangeOffset, (int)copySize));

                    va += copySize;
                    offset += (int)copySize;
                }
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }
            }
        }

        public ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, false);
            }

            if (TryGetVirtualContiguous(va, size, out MemoryBlock memoryBlock, out ulong offset))
            {
                return memoryBlock.GetSpan(offset, size);
            }
            else
            {
                Span<byte> data = new byte[size];

                ReadImpl(va, data);

                return data;
            }
        }

        public WritableRegion GetWritableRegion(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return new WritableRegion(null, va, Memory<byte>.Empty);
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, true);
            }

            if (TryGetVirtualContiguous(va, size, out MemoryBlock memoryBlock, out ulong offset))
            {
                return new WritableRegion(null, va, memoryBlock.GetMemory(offset, size));
            }
            else
            {
                Memory<byte> memory = new byte[size];

                ReadImpl(va, memory.Span);

                return new WritableRegion(this, va, memory);
            }
        }

        public ref T GetRef<T>(ulong va) where T : unmanaged
        {
            if (!TryGetVirtualContiguous(va, Unsafe.SizeOf<T>(), out MemoryBlock memory, out ulong offset))
            {
                ThrowMemoryNotContiguous();
            }

            SignalMemoryTracking(va, (ulong)Unsafe.SizeOf<T>(), true);

            return ref memory.GetRef<T>(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMapped(ulong va)
        {
            return ValidateAddress(va) && _pages.IsMapped(va);
        }

        public bool IsRangeMapped(ulong va, ulong size)
        {
            AssertValidAddressAndSize(va, size);

            return _pages.IsRangeMapped(va, size);
        }

        private static void ThrowMemoryNotContiguous() => throw new MemoryNotContiguousException();

        private bool TryGetVirtualContiguous(ulong va, int size, out MemoryBlock memory, out ulong offset)
        {
            if (_addressSpace.HasAnyPrivateAllocation(va, (ulong)size, out PrivateRange range))
            {
                // If we have a private allocation overlapping the range,
                // then the access is only considered contiguous if it covers the entire range.

                if (range.Memory != null)
                {
                    memory = range.Memory;
                    offset = range.Offset;

                    return true;
                }

                memory = null;
                offset = 0;

                return false;
            }

            memory = _backingMemory;
            offset = GetPhysicalAddressInternal(va);

            return IsPhysicalContiguous(va, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsPhysicalContiguous(ulong va, int size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, (ulong)size))
            {
                return false;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return false;
                }

                if (GetPhysicalAddressInternal(va) + PageSize != GetPhysicalAddressInternal(va + PageSize))
                {
                    return false;
                }

                va += PageSize;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetContiguousSize(ulong va, ulong size)
        {
            ulong contiguousSize = PageSize - (va & PageMask);

            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, size))
            {
                return contiguousSize;
            }

            int pages = GetPagesCount(va, size, out va);

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return contiguousSize;
                }

                if (GetPhysicalAddressInternal(va) + PageSize != GetPhysicalAddressInternal(va + PageSize))
                {
                    return contiguousSize;
                }

                va += PageSize;
                contiguousSize += PageSize;
            }

            return Math.Min(contiguousSize, size);
        }

        private (MemoryBlock, ulong, ulong) GetMemoryOffsetAndSize(ulong va, ulong size)
        {
            PrivateRange privateRange = _addressSpace.GetFirstPrivateAllocation(va, size, out ulong nextVa);

            if (privateRange.Memory != null)
            {
                return (privateRange.Memory, privateRange.Offset, privateRange.Size);
            }

            ulong physSize = GetContiguousSize(va, Math.Min(size, nextVa - va));

            return (_backingMemory, GetPhysicalAddressChecked(va), physSize);
        }

        public IEnumerable<HostMemoryRange> GetHostRegions(ulong va, ulong size)
        {
            if (!ValidateAddressAndSize(va, size))
            {
                return null;
            }

            var regions = new List<HostMemoryRange>();
            ulong endVa = va + size;

            try
            {
                while (va < endVa)
                {
                    (MemoryBlock memory, ulong rangeOffset, ulong rangeSize) = GetMemoryOffsetAndSize(va, endVa - va);

                    regions.Add(new((UIntPtr)memory.GetPointer(rangeOffset, rangeSize), rangeSize));

                    va += rangeSize;
                }
            }
            catch (InvalidMemoryRegionException)
            {
                return null;
            }

            return regions;
        }

        public IEnumerable<MemoryRange> GetPhysicalRegions(ulong va, ulong size)
        {
            if (size == 0)
            {
                return Enumerable.Empty<MemoryRange>();
            }

            return GetPhysicalRegionsImpl(va, size);
        }

        private List<MemoryRange> GetPhysicalRegionsImpl(ulong va, ulong size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, size))
            {
                return null;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            var regions = new List<MemoryRange>();

            ulong regionStart = GetPhysicalAddressInternal(va);
            ulong regionSize = PageSize;

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return null;
                }

                ulong newPa = GetPhysicalAddressInternal(va + PageSize);

                if (GetPhysicalAddressInternal(va) + PageSize != newPa)
                {
                    regions.Add(new MemoryRange(regionStart, regionSize));
                    regionStart = newPa;
                    regionSize = 0;
                }

                va += PageSize;
                regionSize += PageSize;
            }

            regions.Add(new MemoryRange(regionStart, regionSize));

            return regions;
        }

        private void ReadImpl(ulong va, Span<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            try
            {
                AssertValidAddressAndSize(va, (ulong)data.Length);

                ulong endVa = va + (ulong)data.Length;
                int offset = 0;

                while (va < endVa)
                {
                    (MemoryBlock memory, ulong rangeOffset, ulong copySize) = GetMemoryOffsetAndSize(va, (ulong)(data.Length - offset));

                    memory.GetSpan(rangeOffset, (int)copySize).CopyTo(data.Slice(offset, (int)copySize));

                    va += copySize;
                    offset += (int)copySize;
                }
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This function also validates that the given range is both valid and mapped, and will throw if it is not.
        /// </remarks>
        public void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null)
        {
            AssertValidAddressAndSize(va, size);

            if (precise)
            {
                Tracking.VirtualMemoryEvent(va, size, write, precise: true, exemptId);
                return;
            }

            // Software table, used for managed memory tracking.

            _pages.SignalMemoryTracking(Tracking, va, size, write, exemptId);
        }

        /// <summary>
        /// Computes the number of pages in a virtual address range.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <param name="startVa">The virtual address of the beginning of the first page</param>
        /// <remarks>This function does not differentiate between allocated and unallocated pages.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPagesCount(ulong va, ulong size, out ulong startVa)
        {
            // WARNING: Always check if ulong does not overflow during the operations.
            startVa = va & ~(ulong)PageMask;
            ulong vaSpan = (va - startVa + size + PageMask) & ~(ulong)PageMask;

            return (int)(vaSpan / PageSize);
        }

        public RegionHandle BeginTracking(ulong address, ulong size, int id, RegionFlags flags = RegionFlags.None)
        {
            return Tracking.BeginTracking(address, size, id, flags);
        }

        public MultiRegionHandle BeginGranularTracking(ulong address, ulong size, IEnumerable<IRegionHandle> handles, ulong granularity, int id, RegionFlags flags = RegionFlags.None)
        {
            return Tracking.BeginGranularTracking(address, size, handles, granularity, id, flags);
        }

        public SmartMultiRegionHandle BeginSmartGranularTracking(ulong address, ulong size, ulong granularity, int id)
        {
            return Tracking.BeginSmartGranularTracking(address, size, granularity, id);
        }

        private ulong GetPhysicalAddressChecked(ulong va)
        {
            if (!IsMapped(va))
            {
                ThrowInvalidMemoryRegionException($"Not mapped: va=0x{va:X16}");
            }

            return GetPhysicalAddressInternal(va);
        }

        private ulong GetPhysicalAddressInternal(ulong va)
        {
            return _nativePageTable.GetPhysicalAddress(va);
        }

        /// <inheritdoc/>
        public void Reprotect(ulong va, ulong size, MemoryPermission protection)
        {
            // TODO
        }

        /// <inheritdoc/>
        public void TrackingReprotect(ulong va, ulong size, MemoryPermission protection, bool guest)
        {
            if (guest)
            {
                _addressSpace.Reprotect(va, size, protection);
            }
            else
            {
                _pages.TrackingReprotect(va, size, protection);
            }
        }

        /// <summary>
        /// Disposes of resources used by the memory manager.
        /// </summary>
        protected override void Destroy()
        {
            _addressSpace.Dispose();
            _nativePageTable.Dispose();
        }

        protected override Span<byte> GetPhysicalAddressSpan(ulong pa, int size)
            => _backingMemory.GetSpan(pa, size);

        protected override ulong TranslateVirtualAddressForRead(ulong va)
            => GetPhysicalAddressInternal(va);
    }
}
