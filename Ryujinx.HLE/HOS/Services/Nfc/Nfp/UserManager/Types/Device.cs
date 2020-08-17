﻿using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.HLE.HOS.Services.OsTypes;

namespace Ryujinx.HLE.HOS.Services.Nfc.Nfp.UserManager
{
    class Device
    {
        public SystemEventType ActivateEvent;
        public SystemEventType DeactivateEvent;

        public DeviceState State = DeviceState.Unavailable;

        public PlayerIndex Handle;
        public NpadIdType  NpadIdType;
    }
}