using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DbUp;
using QueryLifting;
using Xunit;
using static QueryLifting.SqlHelper;
using static Foo.FooSqlHelper;

namespace Foo
{
    public class Program
    {
        public static string ConnectionString => @"Data Source=(local)\SQL2014;Initial Catalog=QueryLifting;Integrated Security=True";

        static async Task Main()
        {
            Init();

            if (DbUp() != 0) return;

            await DataReaderExample(new DateTime(2015, 1, 1));
            await PostExample(new DateTime(2015, 1, 1));
            await InsertUpdateDelete();
            await NamedMethod(new DateTime(2015, 1, 1));
            ParamExample();
            await PaggingByTwoQueries(new DateTime(2015, 1, 1), 1, 1);
            await PaggingByOneQuery(new DateTime(2015, 1, 1), 1, 1);
            await OptionExample(new DateTime(2015, 1, 1));
            await FuncExample();
            await MyEnumExample();
            await ChoiceExample("test");
            await ParentChildExample();
            await ParentChildExample2();
            await ClusterExample(new DateTime(2015, 1, 1), new DateTime(2018, 1, 1));
        }

        private static async Task DataReaderExample(DateTime? date)
        {
            await new {date}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue)
                    builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                command.CommandText = builder.ToString();
                return command.Query(reader => reader.Read(() => {
                    Console.WriteLine("{0} {1} {2}", reader.Int32(reader.Ordinal("PostId")),
                        reader.String(reader.Ordinal("Text")),
                        reader.DateTime(reader.Ordinal("CreationDate")));
                    return new { };
                }));
            }).Read();
        }

        private static async Task PostExample(DateTime? date)
        {
            foreach (var record in await new {date}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue)
                    builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                command.CommandText = builder.ToString();
                return command.Query<PostInfo>();
            }).Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private class PostData
        {
            public string Text { get; set; }
            public DateTime CreationDate { get; set; }
        }

        private static async Task<int> InsertOrUpdate(int? id, PostData data)
        {
            var param = new {
                data.Text,
                data.CreationDate
            };
            const string table = "Post";
            if (!id.HasValue)
                return await param.Apply(p =>
                    InsertQuery(table, default(int), p)).Single();
            else
            {
                await new {PostId = id.Value, param}.Apply(p =>
                    UpdateQuery(table, new {p.PostId}, p.param)).Execute();
                return id.Value;
            }
        }

        private static async Task InsertUpdateDelete()
        {
            int id;
            {
                id = await InsertOrUpdate(null, new PostData {Text = "Test", CreationDate = DateTime.Now});
                Console.WriteLine(id);
                Assert.Equal("Test",
                    await new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Query<string>()).Single());
            }
            {
                await InsertOrUpdate(id, new PostData {Text = "Test2", CreationDate = DateTime.Now});
                Assert.Equal("Test2",
                    await new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Query<string>()).Single());
            }
            {
                var rowsNumber = await new {PostId = id}.Apply(p =>
                    DeleteQuery("Post", p)).Execute();
                Assert.Equal(1, rowsNumber);
                Console.WriteLine(rowsNumber);
                Assert.Empty(
                    await new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Query<string>()).Read());
            }
        }

        private static async Task NamedMethod(DateTime? date)
        {
            foreach (var record in await ReadPosts(date).Read())
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
        }

        public static Query<List<PostInfo>> ReadPosts(DateTime? date)
        {
            var command = new SqlCommand();
            var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
            if (date.HasValue)
                builder.Append(command, @"
    AND CreationDate > @date", new {date});
            command.CommandText = builder.ToString();
            return command.Query<PostInfo>();
        }

        private static void ParamExample()
        {
            new {C1 = new DateTime?().Param()}.Apply(p =>
                new SqlCommand("INSERT T001 (C1) VALUES (@C1)").AddParams(p).NonQuery());
        }

        private static async Task PaggingByTwoQueries(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = new {date, offset, pageSize}.Apply(p => PagedQueries<PostInfo>(
                (builder, command) => {
                    builder.Append(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                    if (p.date.HasValue)
                        builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                },
                (builder, command) => builder.Append("CreationDate DESC, PostId"), p.offset, p.pageSize));
            foreach (var record in await paggingInfo.Data.Read())
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            Console.WriteLine($"{paggingInfo.Count.Read()}");
        }

        private static async Task PaggingByOneQuery(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = await new {date, offset, pageSize}.Apply(p => PagedQuery<PostInfo>(
                (builder, command) => {
                    builder.Append(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                    if (p.date.HasValue)
                        builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                },
                (builder, command) => builder.Append("CreationDate DESC, PostId"), p.offset, p.pageSize)).Read();
            foreach (var record in paggingInfo.Data)
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            Console.WriteLine($"{paggingInfo.Count}");
        }

        private static async Task OptionExample(Option<DateTime> date)
        {
            foreach (var record in await new {date}.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue)
                    builder.Append(command, @"
    AND CreationDate > @date", new {date = p.date.Value});
                command.CommandText = builder.ToString();
                return command.Query<PostInfo>();
            }).Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task ChoiceExample(Choice<string, DateTime> textOrDate)
        {
            foreach (var record in await new {textOrDate}.Apply(p => {
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
                return command.Query<PostInfo>();
            }).Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task FuncExample()
        {
            foreach (var record in await new {date = Func.New(() => DateTime.Now)}.Apply(p => new SqlCommand(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE CreationDate > @date").AddParams(new {date = p.date()}).Query<PostInfo>()).Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task MyEnumExample()
        {
            var single = await new {a = MyEnum.A}
                .Apply(p => new SqlCommand(@"SELECT @a AS a").AddParams(p).Query<MyEnum?>()).Single();
            Assert.Equal(MyEnum.A, single);
        }

        private static async Task ParentChildExample(int maxParentId = 10)
        {
            var result = await new {maxParentId}.Apply(p => {
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
                return command.Query(async reader => {
                    var parents = await reader.Read<Parent>();
                    await reader.NextResultAsync();
                    return new {
                        parents,
                        children = await reader.Read<Child>()
                    };
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

        private static async Task ParentChildExample2(int maxChildId = 10)
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
            var result = await Transaction(IsolationLevel.Snapshot, async transaction => new {
                children = await queries.child.Read(transaction),
                parents = await queries.parent.Read(transaction)
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

        private static async Task ClusterExample(DateTime? startDate, DateTime? endDate)
        {
            foreach (var record in await new {
                startDate = startDate.Cluster(),
                endDate = endDate.Cluster()
            }.Apply(p => {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.startDate.Value.HasValue)
                    builder.Append(command, @"
    AND CreationDate >= @startDate", new {p.startDate});
                if (p.endDate.Value.HasValue)
                    builder.Append(command, @"
    AND CreationDate <= @endDate", new {p.endDate});
                command.CommandText = builder.ToString();
                return command.Query<PostInfo>();
            }).Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        public static void Init()
        {
            ConnectionStringFunc = () => ConnectionString;
        }

        private static int DbUp()
        {
            SetAllowSnapshotIsolation();

            var connectionString = ConnectionString;

            var upgrader =
                DeployChanges.To
                    .SqlDatabase(connectionString)
                    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                    .WithTransactionPerScript()
                    .LogToConsole()
                    .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(result.Error);
                Console.ResetColor();
#if DEBUG
                Console.ReadLine();
#endif
                return -1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success!");
            Console.ResetColor();
            return 0;
        }

        private static void SetAllowSnapshotIsolation()
        {
            new SqlCommand(@"
IF ((SELECT snapshot_isolation_state_desc FROM sys.databases WHERE name='QueryLifting') <> 'ON')
BEGIN
  ALTER DATABASE QueryLifting SET ALLOW_SNAPSHOT_ISOLATION ON
END;").NonQuery().Execute().Wait();
        }
    }

    public enum MyEnum
    {
        A,
        B
    }
}
