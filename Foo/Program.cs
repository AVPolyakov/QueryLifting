using System;
using System.Data.SqlClient;
using System.Text;
using QueryLifting;

namespace Foo
{
    public class Program
    {
        static void Main()
        {
            SqlUtil.ConnectionStringFunc = () => ConnectionString;

            M1();
        }

        private static void M1()
        {
            foreach (var record in new {date = (DateTime?) new DateTime(2015, 1, 1)}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                command.CommandText = builder.ToString();
                return command.Read<A001>();
            }))
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        //Scripts for database are located in folder DatabaseScripts in project root.
        public static string ConnectionString => @"Data Source=(local)\SQL2014;Initial Catalog=QueryLifting;Integrated Security=True";
    }
}
