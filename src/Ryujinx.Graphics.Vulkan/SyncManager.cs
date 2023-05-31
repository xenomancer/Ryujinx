using Ryujinx.Common.Logging;
using Silk.NET.Vulkan;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ryujinx.Graphics.Vulkan
{
    class SyncManager
    {
        private sealed class SyncHandle
        {
            public ulong ID;
            public MultiFenceHolder Waitable;
            public ulong FlushId;
            public bool Signalled;

            public bool NeedsFlush(ulong currentFlushId)
            {
                return (long)(FlushId - currentFlushId) >= 0;
            }
        }

        private ulong _firstHandle = 0;

        private readonly VulkanRenderer _gd;
        private readonly Device _device;
        private List<SyncHandle> _handles;
        private ulong FlushId;
        private long WaitTicks;

        public SyncManager(VulkanRenderer gd, Device device)
        {
            _gd = gd;
            _device = device;
            _handles = new List<SyncHandle>();
        }

        public void RegisterFlush()
        {
            FlushId++;
        }

        public void Create(ulong id, bool strict)
        {
            ulong flushId = FlushId;
            MultiFenceHolder waitable = new MultiFenceHolder();
            if (strict || _gd.InterruptAction == null)
            {
                _gd.FlushAllCommands();
                _gd.CommandBufferPool.AddWaitable(waitable);
            }
            else
            {
                // Don't flush commands, instead wait for the current command buffer to finish.
                // If this sync is waited on before the command buffer is submitted, interrupt the gpu thread and flush it manually.

                _gd.CommandBufferPool.AddInUseWaitable(waitable);
            }

            SyncHandle handle = new SyncHandle
            {
                ID = id,
                Waitable = waitable,
                FlushId = flushId
            };

            lock (_handles)
            {
                _handles.Add(handle);
            }
        }

        public ulong GetCurrent()
        {
            lock (_handles)
            {
                ulong lastHandle = _firstHandle;

                foreach (SyncHandle handle in _handles)
                {
                    lock (handle)
                    {
                        if (handle.Waitable == null)
                        {
                            continue;
                        }

                        if (handle.ID > lastHandle)
                        {
                            bool signaled = handle.Signalled || handle.Waitable.WaitForFences(_gd.Api, _device, 0);
                            if (signaled)
                            {
                                lastHandle = handle.ID;
                                handle.Signalled = true;
                            }
                        }
                    }
                }

                return lastHandle;
            }
        }

        public void Wait(ulong id)
        {
            SyncHandle result = null;

            lock (_handles)
            {
                if ((long)(_firstHandle - id) > 0)
                {
                    return; // The handle has already been signalled or deleted.
                }

                foreach (SyncHandle handle in _handles)
                {
                    if (handle.ID == id)
                    {
                        result = handle;
                        break;
                    }
                }
            }

            if (result != null)
            {
                if (result.Waitable == null)
                {
                    return;
                }

                long beforeTicks = Stopwatch.GetTimestamp();

                if (result.NeedsFlush(FlushId))
                {
                    _gd.InterruptAction(() =>
                    {
                        if (result.NeedsFlush(FlushId))
                        {
                            _gd.FlushAllCommands();
                        }
                    });
                }

                lock (result)
                {
                    if (result.Waitable == null)
                    {
                        return;
                    }

                    bool signaled = result.Signalled || result.Waitable.WaitForFences(_gd.Api, _device, 1000000000);

                    if (!signaled)
                    {
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"VK Sync Object {result.ID} failed to signal within 1000ms. Continuing...");
                    }
                    else
                    {
                        WaitTicks += Stopwatch.GetTimestamp() - beforeTicks;
                        result.Signalled = true;
                    }
                }
            }
        }

        public void Cleanup()
        {
            // Iterate through handles and remove any that have already been signalled.

            while (true)
            {
                SyncHandle first = null;
                lock (_handles)
                {
                    first = _handles.FirstOrDefault();
                }

                if (first == null || first.NeedsFlush(FlushId)) break;

                bool signaled = first.Waitable.WaitForFences(_gd.Api, _device, 0);
                if (signaled)
                {
                    // Delete the sync object.
                    lock (_handles)
                    {
                        lock (first)
                        {
                            _firstHandle = first.ID + 1;
                            _handles.RemoveAt(0);
                            first.Waitable = null;
                        }
                    }
                } else
                {
                    // This sync handle and any following have not been reached yet.
                    break;
                }
            }
        }

        public long GetAndResetWaitTicks()
        {
            long result = WaitTicks;
            WaitTicks = 0;

            return result;
        }
    }
}
