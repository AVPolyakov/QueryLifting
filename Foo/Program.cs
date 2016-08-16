using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QueryLifting;
using static QueryLifting.SqlUtil;
using static Foo.FooSqlUtil;

namespace Foo
{
    public class Program
    {
        private static void M1(DateTime? date)
        {
            new {date}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                command.CommandText = builder.ToString();
                return command.Read(reader => {
                    Console.WriteLine("{0} {1} {2}", reader.Int32(reader.GetOrdinal("PostId")),
                        reader.OptionString(reader.GetOrdinal("Text")),
                        reader.DateTime(reader.GetOrdinal("CreationDate")));
                    return new {};
                });
            }).ToList();
        }

        private static void M2(DateTime? date)
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

        private static void M3()
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

        private static void M4(DateTime? date)
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

        private static void M5()
        {
            new {C1 = new DateTime?().Param()}.Apply(p =>
                new SqlCommand("INSERT T001 (C1) VALUES (@C1)").AddParams(p).NonQuery());
        }

        private static void M6(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = new {date, offset, pageSize}.Apply(p => PagedQueries<A001>(
                (builder, command) => {
                    builder.Append(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                    if (p.date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                },
                (builder, command) => builder.Append("CreationDate DESC, PostId"), p.offset, p.pageSize));
            foreach (var record in paggingInfo.Data)
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            Console.WriteLine($"{paggingInfo.Count.Read()}");
        }

        private static void M7(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = new {date, offset, pageSize}.Apply(p => PagedQuery<A001>(
                (builder, command) => {
                    builder.Append(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                    if (p.date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                },
                (builder, command) => builder.Append("CreationDate DESC, PostId"), p.offset, p.pageSize)).Read();
            foreach (var record in paggingInfo.Data)
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            Console.WriteLine($"{paggingInfo.Count}");
        }

        private static void M8()
        {
            foreach (var record in new {date = Func.New(() => DateTime.Now)}.Apply(p => new SqlCommand(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE CreationDate > @date").AddParams(new {date = p.date()}).Read<A001>()))
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static void M9()
        {
            var single = new {a = MyEnum.A}
                .Apply(p => new SqlCommand(@"SELECT @a AS a").AddParams(p).Read<Option<MyEnum>>()).Single();
            Assert.AreEqual(MyEnum.A, single);
        }

        static void Main()
        {
            Init();

            M1(new DateTime(2015, 1, 1));
            M2(new DateTime(2015, 1, 1));
            M3();
            M4(new DateTime(2015, 1, 1));
            M5();
            M6(new DateTime(2015, 1, 1), 1, 1);
            M7(new DateTime(2015, 1, 1), 1, 1);
            M8();
            M9();
        }

        public static void Init()
        {
            ConnectionStringFunc = () => ConnectionString;
            AddParamsMethods.Add(typeof (MyEnum), GetMethodInfo<Func<SqlCommand, string, MyEnum, SqlParameter>>(
                (command, name, value) => command.AddParam(name, value)));
            MethodInfos.Add(typeof (Option<MyEnum>), GetMethodInfo<Func<SqlDataReader, int, Option<MyEnum>>>(
                (reader, i) => reader.OptionMyEnum(i)));
        }

        //Scripts for database are located in folder DatabaseScripts in project root.
        public static string ConnectionString => @"Data Source=(local)\SQL2014;Initial Catalog=QueryLifting;Integrated Security=True";
    }

    public enum MyEnum
    {
        A,
        B
    }
}
