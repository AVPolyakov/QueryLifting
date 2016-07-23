using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QueryLifting;
using static QueryLifting.SqlUtil;

namespace Foo
{
    public class Program
    {
        static void Main()
        {
            ConnectionStringFunc = () => ConnectionString;

            M1(new DateTime(2015, 1, 1));
            M2();
            M3(new DateTime(2015, 1, 1));
        }

        private static void M1(DateTime? date)
        {
            foreach (var record in new {date}.Apply(p => {
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

        private static void M2()
        {
            var id = new {Text = "Test", CreationDate = DateTime.Now}.Apply(p =>
                InsertQuery(default(int), "Post", p)).Read().Single();
            Console.WriteLine(id);
            Assert.AreEqual("Test",
                new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                    .Read<Option<string>>()).Single());
            {
                var rowsNumber = new {PostId = id, Text = "Test2", CreationDate = DateTime.Now}.Apply(p =>
                    UpdateQuery("Post", p)).Execute();
                Assert.AreEqual(1, rowsNumber);
                Console.WriteLine(rowsNumber);
                Assert.AreEqual("Test2",
                    new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Read<Option<string>>()).Single());
            }
            {
                var rowsNumber = new {PostId = id}.Apply(p =>
                    DeleteQuery("Post", p)).Execute();
                Assert.AreEqual(1, rowsNumber);
                Console.WriteLine(rowsNumber);
                Assert.AreEqual(0,
                    new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Read<Option<string>>()).Count());
            }
        }

        private static void M3(DateTime? date)
        {
            foreach (var record in ReadPosts(date))
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
        }

        public static IEnumerable<A001> ReadPosts(DateTime? date)
        {
            var command = new SqlCommand();
            var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
            if (date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {date});
            command.CommandText = builder.ToString();
            return command.Read<A001>();
        }

        //Scripts for database are located in folder DatabaseScripts in project root.
        public static string ConnectionString => @"Data Source=(local)\SQL2014;Initial Catalog=QueryLifting;Integrated Security=True";
    }
}
