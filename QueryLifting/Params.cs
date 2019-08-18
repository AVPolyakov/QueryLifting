using System.Data.SqlClient;

namespace QueryLifting
{
    public struct Params<T>
    {
        private readonly T value;

        public Params(T value) => this.value = value;

        internal void AddParams(SqlCommand command) => command.AddParams(value);
    }

    public static class ParamsExtensions
    {
        public static Params<T> Params<T>(this T it) => new Params<T>(it);
    }
}