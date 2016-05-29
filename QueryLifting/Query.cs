using System;
using System.Data.SqlClient;

namespace QueryLifting
{
    public class Query<T>
    {
        public SqlCommand Command { get; }
        public Func<SqlDataReader, T> ReaderFunc { get; }
        public Option<string> ConnectionString { get; }

        internal Query(SqlCommand command, Func<SqlDataReader, T> readerFunc, Option<string> connectionString)
        {
            Command = command;
            ReaderFunc = readerFunc;
            ConnectionString = connectionString;
            if (SqlUtil.QueryChecker != null) SqlUtil.QueryChecker.Query(this);
        }
    }
}