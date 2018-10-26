namespace Ryujinx.HLE.Loaders.Npdm
{
    internal struct KernelAccessControlIrq
    {
        public uint Irq0 { get; private set; }
        public uint Irq1 { get; private set; }

        public KernelAccessControlIrq(uint irq0, uint irq1)
        {
            Irq0 = irq0;
            Irq1 = irq1;
        }
    }
}