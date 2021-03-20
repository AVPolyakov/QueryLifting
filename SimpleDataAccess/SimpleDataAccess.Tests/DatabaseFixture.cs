using System;
using System.Reflection;
using DbUp;

namespace SimpleDataAccess.Tests
{
    public class DatabaseFixture
    {
        public static readonly ConnectionInfo Db = new(@"Data Source=(local)\SQL2014;Initial Catalog=SimpleDataAccess;Integrated Security=True");
        
        public DatabaseFixture()
        {
            DbUp();
        }
        
        private void DbUp()
        {
            var upgrader = DeployChanges.To
                .SqlDatabase(Db.ConnectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .WithTransactionPerScript()
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
                throw new Exception("Database upgrade failed", result.Error);
        }
    }
}