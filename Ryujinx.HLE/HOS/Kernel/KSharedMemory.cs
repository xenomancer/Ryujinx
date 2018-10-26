namespace Ryujinx.HLE.HOS.Kernel
{
    internal class KSharedMemory
    {
        public long Pa   { get; private set; }
        public long Size { get; private set; }

        public KSharedMemory(long pa, long size)
        {
            Pa   = pa;
            Size = size;
        }
    }
}