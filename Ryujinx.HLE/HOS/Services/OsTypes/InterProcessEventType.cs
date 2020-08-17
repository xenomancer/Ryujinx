﻿namespace Ryujinx.HLE.HOS.Services.OsTypes
{
    struct InterProcessEventType
    {
        public enum InitializationState : byte
        {
            NotInitialized,
            Initialized
        }

        public readonly bool AutoClear;
        public InitializationState State;
        public bool ReadableHandleManaged;
        public bool WritableHandleManaged;
        public int ReadableHandle;
        public int WritableHandle;

        public InterProcessEventType(
            bool autoClear,
            bool readableHandleManaged,
            bool writableHandleManaged,
            int readableHandle,
            int writableHandle)
        {
            AutoClear = autoClear;
            State = InitializationState.Initialized;
            ReadableHandleManaged = readableHandleManaged;
            WritableHandleManaged = writableHandleManaged;
            ReadableHandle = readableHandle;
            WritableHandle = writableHandle;
        }
    }
}
