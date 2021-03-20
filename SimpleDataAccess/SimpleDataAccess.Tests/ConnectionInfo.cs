namespace SimpleDataAccess.Tests
{
    public class ConnectionInfo : IConnectionInfo
    {
        public string ConnectionString { get; }

        public ConnectionInfo(string connectionString) => ConnectionString = connectionString;
    }
}