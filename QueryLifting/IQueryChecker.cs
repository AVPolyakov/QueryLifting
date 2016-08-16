using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace QueryLifting
{
    public interface IQueryChecker
    {
        void Query<T>(Query<T> query);
        void NonQuery(NonQuery query);
        IEnumerable<T> Read<T>(SqlDataReader reader, Func<T> materializer);
        T Check<T>(SqlDataReader reader, int ordinal);
        int GetOrdinal(SqlDataReader reader, string name);
    }
}