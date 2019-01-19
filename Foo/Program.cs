using System;
using System.Collections.Generic;
using System.Data;
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
        static void Main()
        {
            Init();

            DataReaderExample(new DateTime(2015, 1, 1));
            PostExample(new DateTime(2015, 1, 1));
            InsertUpdateDelete();
            NamedMethod(new DateTime(2015, 1, 1));
            ParamExample();
            PaggingByTwoQueries(new DateTime(2015, 1, 1), 1, 1);
            PaggingByOneQuery(new DateTime(2015, 1, 1), 1, 1);
            OptionExample(new DateTime(2015, 1, 1));
            FuncExample();
            MyEnumExample();
            ChoiceExample("test");
            ParentChildExample();
            ParentChildExample2();
        }

        private static void DataReaderExample(DateTime? date)
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
                        reader.String(reader.GetOrdinal("Text")),
                        reader.DateTime(reader.GetOrdinal("CreationDate")));
                    return new {};
                });
            }).ToList();
        }

        private static void PostExample(DateTime? date)
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
                return command.Read<PostInfo>();
            }))
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private class PostData
        {
            public string Text { get; set; }
            public DateTime CreationDate { get; set; }
        }

        private static int InsertOrUpdate(int? id, PostData data)
        {
            var param = new {
                data.Text, 
                data.CreationDate
            };
            const string table = "Post";
            if (!id.HasValue)
                return param.Apply(p => 
                    InsertQuery(table, default(int), p)).Read().Single();
            else
            {
                new {PostId = id.Value, param}.Apply(p =>
                    UpdateQuery(table, new {p.PostId}, p.param)).Execute();
                return id.Value;
            }
        }

        private static void InsertUpdateDelete()
        {
            int id;
            {
                id = InsertOrUpdate(null, new PostData {Text = "Test", CreationDate = DateTime.Now});
                Console.WriteLine(id);
                Assert.AreEqual("Test",
                    new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Read<string>()).Single());
            }
            {
                InsertOrUpdate(id, new PostData {Text = "Test2", CreationDate = DateTime.Now});
                Assert.AreEqual("Test2",
                    new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Read<string>()).Single());
            }
            {
                var rowsNumber = new {PostId = id}.Apply(p =>
                    DeleteQuery("Post", p)).Execute();
                Assert.AreEqual(1, rowsNumber);
                Console.WriteLine(rowsNumber);
                Assert.AreEqual(0,
                    new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Read<string>()).Count());
            }
        }

        private static void NamedMethod(DateTime? date)
        {
            foreach (var record in ReadPosts(date))
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
        }

        public static IEnumerable<PostInfo> ReadPosts(DateTime? date)
        {
            var command = new SqlCommand();
            var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
            if (date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {date});
            command.CommandText = builder.ToString();
            return command.Read<PostInfo>();
        }

        private static void ParamExample()
        {
            new {C1 = new DateTime?().Param()}.Apply(p =>
                new SqlCommand("INSERT T001 (C1) VALUES (@C1)").AddParams(p).NonQuery());
        }

        private static void PaggingByTwoQueries(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = new {date, offset, pageSize}.Apply(p => PagedQueries<PostInfo>(
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

        private static void PaggingByOneQuery(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = new {date, offset, pageSize}.Apply(p => PagedQuery<PostInfo>(
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

        private static void OptionExample(Option<DateTime> date)
        {
            foreach (var record in new {date}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue) builder.Append(command, @"
    AND CreationDate > @date", new {date = p.date.Value});
                command.CommandText = builder.ToString();
                return command.Read<PostInfo>();
            }))
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static void ChoiceExample(Choice<string, DateTime> textOrDate)
        {
            foreach (var record in new {textOrDate}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                p.textOrDate.Match(text => builder.Append(command, @"
    AND Text LIKE @text", new {text = $"%{text}%"}),
                    date => builder.Append(command, @"
    AND CreationDate > @date", new {date}));
                command.CommandText = builder.ToString();
                return command.Read<PostInfo>();
            }))
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static void FuncExample()
        {
            foreach (var record in new {date = Func.New(() => DateTime.Now)}.Apply(p => new SqlCommand(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE CreationDate > @date").AddParams(new {date = p.date()}).Read<PostInfo>()))
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static void MyEnumExample()
        {
            var single = new {a = MyEnum.A}
                .Apply(p => new SqlCommand(@"SELECT @a AS a").AddParams(p).Read<MyEnum?>()).Single();
            Assert.AreEqual(MyEnum.A, single);
        }

        private static void ParentChildExample(int maxParentId = 10)
        {
            var result = new {maxParentId}.Apply(p => {
                var command = new SqlCommand();
                var parent = @"
SELECT *
FROM Parent
WHERE ParentId <= @maxParentId";
                command.AddParams(new {p.maxParentId});
                command.CommandText = $@"
{parent};
SELECT *
FROM Child
WHERE ParentId IN (SELECT ParentId FROM ({parent}) T);";
                return command.Query(reader => new {
                    parents = reader.Read<Parent>().ToList(),
                    children = reader.ReadNext<Child>().ToList()
                });
            }).Read();
            var parentChild = result.children.ToLookup(_ => _.ParentId);
            foreach (var parent in result.parents)
            {
                Console.WriteLine(parent.ParentId);
                foreach (var child in parentChild[parent.ParentId])
                    Console.WriteLine($"  {child.ChildId}");
            }
            var childParent = result.parents.ToDictionary(_ => _.ParentId);
            foreach (var child in result.children)
                Console.WriteLine($"{child.ChildId} {childParent[child.ParentId].ParentId}");
        }

        private static void ParentChildExample2(int maxChildId = 10)
        {
            var queries = new {maxChildId}.Apply(p => {
                var child = QueryAction((builder, command) => builder.Append(command, @"
SELECT *
FROM Child
WHERE ChildId <= @maxChildId", new {p.maxChildId}));
                return new {
                    child = GetCommand(child).Query<Child>(),
                    parent = GetCommand((builder, command) => builder.Append($@"
SELECT *
FROM Parent
WHERE ParentId IN (SELECT ParentId FROM ({Text(child, command)}) T)")).Query<Parent>()
                };
            });
            var result = Transaction(IsolationLevel.Snapshot, transaction => new {
                children = queries.child.Read(transaction).ToList(),
                parents = queries.parent.Read(transaction).ToList()
            });
            var parentChild = result.children.ToLookup(_ => _.ParentId);
            foreach (var parent in result.parents)
            {
                Console.WriteLine(parent.ParentId);
                foreach (var child in parentChild[parent.ParentId])
                    Console.WriteLine($"  {child.ChildId}");
            }
            var childParent = result.parents.ToDictionary(_ => _.ParentId);
            foreach (var child in result.children)
                Console.WriteLine($"{child.ChildId} {childParent[child.ParentId].ParentId}");
        }

        public static void Init()
        {
            ConnectionStringFunc = () => ConnectionString;
            AddParamsMethods.Add(typeof (MyEnum), GetMethodInfo<Func<SqlCommand, string, MyEnum, SqlParameter>>(
                (command, name, value) => command.AddParam(name, value)));
            MethodInfos.Add(typeof (MyEnum?), GetMethodInfo<Func<SqlDataReader, int, MyEnum?>>(
                (reader, i) => reader.NullableMyEnum(i)));
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
