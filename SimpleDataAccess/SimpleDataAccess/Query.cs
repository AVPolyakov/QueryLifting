using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace SimpleDataAccess
{
    public class Query
    {
        public StringBuilder StringBuilder { get; } = new();

        public List<Action<SqlCommand>> DbCommandActions { get; } = new();

        public IConnectionInfo ConnectionInfo { get; }

        public Query(IConnectionInfo connectionInfo) => ConnectionInfo = connectionInfo;
    }
}