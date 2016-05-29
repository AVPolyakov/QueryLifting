using System.Data.SqlClient;

namespace QueryLifting
{
    public class NonQuery
    {
        public Option<string> ConnectionString { get; }
        public SqlCommand Command { get; }

        internal NonQuery(SqlCommand command, Option<string> connectionString)
        {
            Command = command;
            ConnectionString = connectionString;
            if (SqlUtil.QueryChecker != null) SqlUtil.QueryChecker.NonQuery(this);
        }
    }
}