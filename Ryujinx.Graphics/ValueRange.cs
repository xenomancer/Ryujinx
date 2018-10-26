namespace Ryujinx.Graphics
{
    internal struct ValueRange<T>
    {
        public long Start { get; private set; }
        public long End   { get; private set; }

        public T Value { get; set; }

        public ValueRange(long start, long end, T value = default(T))
        {
            Start = start;
            End   = end;
            Value = value;
        }
    }
}