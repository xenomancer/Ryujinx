namespace Ryujinx.HLE.Loaders.Elf
{
    internal struct ElfSymbol
    {
        public string Name { get; private set; }

        public ElfSymbolType       Type       { get; private set; }
        public ElfSymbolBinding    Binding    { get; private set; }
        public ElfSymbolVisibility Visibility { get; private set; }

        public bool IsFuncOrObject =>
            Type == ElfSymbolType.SttFunc ||
            Type == ElfSymbolType.SttObject;

        public bool IsGlobalOrWeak =>
            Binding == ElfSymbolBinding.StbGlobal ||
            Binding == ElfSymbolBinding.StbWeak;

        public int  ShIdx { get; private set; }
        public long Value { get; private set; }
        public long Size  { get; private set; }

        public ElfSymbol(
            string name,
            int    info,
            int    other,
            int    shIdx,
            long   value,
            long   size)
        {
            Name       = name;
            Type       = (ElfSymbolType)(info & 0xf);
            Binding    = (ElfSymbolBinding)(info >> 4);
            Visibility = (ElfSymbolVisibility)other;
            ShIdx      = shIdx;
            Value      = value;
            Size       = size;
        }
    }
}