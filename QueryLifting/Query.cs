using System;
using System.Data.SqlClient;

namespace QueryLifting
{
    public class Query<T>
    {
        public SqlCommand Command { get; }
        public Func<SqlDataReader, T> ReaderFunc { get; }
        public Option<string> ConnectionString { get; }
        public int Line { get; }
        public string FilePath { get; }

        internal Query(SqlCommand command, Func<SqlDataReader, T> readerFunc, Option<string> connectionString,
            int line, string filePath)
        {
            Command = command;
            ReaderFunc = readerFunc;
            ConnectionString = connectionString;
            Line = line;
            FilePath = filePath;
            if (SqlUtil.QueryChecker != null) SqlUtil.QueryChecker.Query(this);
        }
    }
}