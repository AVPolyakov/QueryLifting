namespace QueryLifting
{
    public struct Params<T>
    {
        internal T Value { get; }

        public Params(T value) => Value = value;
    }

    public static class ParamsExtensions
    {
        public static Params<T> Params<T>(this T it) => new Params<T>(it);
    }
}