namespace QueryLifting
{
    public struct Param<T>
    {
        internal T Value { get; }

        public Param(T value)
        {
            Value = value;
        }
    }

    public static class ParamExtensions
    {
        public static Param<T> Param<T>(this T it) => new Param<T>(it);
    }
}