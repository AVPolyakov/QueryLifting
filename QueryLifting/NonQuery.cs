using System.Data.SqlClient;

namespace QueryLifting
{
    public class NonQuery
    {
        public Option<string> ConnectionString { get; }
        public int Line { get; }
        public string FilePath { get; }
        public SqlCommand Command { get; }

        internal NonQuery(SqlCommand command, Option<string> connectionString,
            int line, string filePath)
        {
            Command = command;
            ConnectionString = connectionString;
            Line = line;
            FilePath = filePath;
            if (SqlHelper.QueryChecker != null) SqlHelper.QueryChecker.NonQuery(this);
        }
    }
}