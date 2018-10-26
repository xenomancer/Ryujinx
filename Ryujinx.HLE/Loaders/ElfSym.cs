namespace Ryujinx.HLE.Loaders
{
    internal struct ElfSym
    {
        public string Name { get; private set; }

        public ElfSymType       Type       { get; private set; }
        public ElfSymBinding    Binding    { get; private set; }
        public ElfSymVisibility Visibility { get; private set; }

        public bool IsFuncOrObject =>
            Type == ElfSymType.SttFunc ||
            Type == ElfSymType.SttObject;

        public bool IsGlobalOrWeak =>
            Binding == ElfSymBinding.StbGlobal ||
            Binding == ElfSymBinding.StbWeak;

        public int  ShIdx { get; private set; }
        public long Value { get; private set; }
        public long Size  { get; private set; }

        public ElfSym(
            string name,
            int    info,
            int    other,
            int    shIdx,
            long   value,
            long   size)
        {
            Name       = name;
            Type       = (ElfSymType)(info & 0xf);
            Binding    = (ElfSymBinding)(info >> 4);
            Visibility = (ElfSymVisibility)other;
            ShIdx      = shIdx;
            Value      = value;
            Size       = size;
        }
    }
}