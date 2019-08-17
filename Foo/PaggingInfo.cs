namespace Foo
{
    public class PaggingInfo<TData, TCount>
    {
        public TData Data { get; }
        public TCount Count { get; }

        public PaggingInfo(TData data, TCount count)
        {
            Data = data;
            Count = count;
        }
    }

    public static class PaggingInfo
    {
        public static PaggingInfo<TData, TCount> Create<TData, TCount>(TData data, TCount count)
            => new PaggingInfo<TData, TCount>(data, count);
    }
}