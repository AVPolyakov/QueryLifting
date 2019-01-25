namespace QueryLifting
{
    /// <summary>
    /// http://www.dbdelta.com/addwithvalue-is-evil/
    /// </summary>
    public struct Varchar
    {
        public string Value { get; }

        public Varchar(string value) => Value = value;
    }

    public static class VarcharExtensions
    {
        public static Varchar Varchar(this string it) => new Varchar(it);
    }
}