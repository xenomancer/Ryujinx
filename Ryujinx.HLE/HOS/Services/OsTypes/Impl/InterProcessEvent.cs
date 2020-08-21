﻿using Ryujinx.Horizon.Kernel.Common;

namespace Ryujinx.HLE.HOS.Services.OsTypes.Impl
{
    static class InterProcessEvent
    {
        public static KernelResult Create(ref InterProcessEventType ipEvent, EventClearMode clearMode)
        {
            KernelResult result = InterProcessEventImpl.Create(out int writableHandle, out int readableHandle);

            if (result != KernelResult.Success)
            {
                return result;
            }

            ipEvent = new InterProcessEventType(
                clearMode == EventClearMode.AutoClear,
                true,
                true,
                readableHandle,
                writableHandle);

            return KernelResult.Success;
        }

        public static void Destroy(ref InterProcessEventType ipEvent)
        {
            ipEvent.State = InterProcessEventType.InitializationState.NotInitialized;

            if (ipEvent.ReadableHandleManaged)
            {
                if (ipEvent.ReadableHandle != 0)
                {
                    InterProcessEventImpl.Close(ipEvent.ReadableHandle);
                }
                ipEvent.ReadableHandleManaged = false;
            }

            if (ipEvent.WritableHandleManaged)
            {
                if (ipEvent.WritableHandle != 0)
                {
                    InterProcessEventImpl.Close(ipEvent.WritableHandle);
                }
                ipEvent.WritableHandleManaged = false;
            }
        }

        public static int DetachReadableHandle(ref InterProcessEventType ipEvent)
        {
            int handle = ipEvent.ReadableHandle;

            ipEvent.ReadableHandle = 0;
            ipEvent.ReadableHandleManaged = false;

            return handle;
        }

        public static int DetachWritableHandle(ref InterProcessEventType ipEvent)
        {
            int handle = ipEvent.WritableHandle;

            ipEvent.WritableHandle = 0;
            ipEvent.WritableHandleManaged = false;

            return handle;
        }

        public static int GetReadableHandle(ref InterProcessEventType ipEvent)
        {
            return ipEvent.ReadableHandle;
        }

        public static int GetWritableHandle(ref InterProcessEventType ipEvent)
        {
            return ipEvent.WritableHandle;
        }

        public static void Signal(ref InterProcessEventType ipEvent)
        {
            InterProcessEventImpl.Signal(ipEvent.WritableHandle);
        }

        public static void Clear(ref InterProcessEventType ipEvent)
        {
            InterProcessEventImpl.Clear(ipEvent.ReadableHandle == 0 ? ipEvent.WritableHandle : ipEvent.ReadableHandle);
        }
    }
}
