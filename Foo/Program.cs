using System;
using System.Collections.Generic;
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

            await PostExample(new DateTime(2015, 1, 1));
            await NamedMethod(new DateTime(2015, 1, 1));
            await InsertUpdateDelete();
            await Pagging(new DateTime(2015, 1, 1), 1, 1);
            await ParentChildExample();
            await MyEnumExample();
            await FuncExample();
            await ChoiceExample("test");
            await ClusterExample(new DateTime(2015, 1, 1), new DateTime(2018, 1, 1));
            await DataReaderExample(new DateTime(2015, 1, 1));
        }
        
        private static async Task PostExample(DateTime? date)
        {
            var query = new {date}.ApplyDynamic(p =>
            {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue)
                    builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                command.CommandText = builder.ToString();
                return command.Query<Post>();
            });
            foreach (var record in await query.Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task NamedMethod(DateTime? date)
        {
            foreach (var record in await PostQuery(date).Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        public static Query<List<Post>> PostQuery(DateTime? date)
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
            return command.Query<Post>();
        }
        
        private static async Task InsertUpdateDelete()
        {
            int id;
            {
                var post = new Post {CreationDate = DateTime.Now};
                FillPost(post, new PostData {Text = "Test"});
                id = await post.Apply(p => InsertQuery(default(int), p)).Single();
                Assert.Equal("Test",
                    await new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Query<string>()).Single());
            }
            {
                var post = await new {PostId = id}.Apply(p =>
                        GetByKeyQuery(default(Post), p)).Single();
                FillPost(post, new PostData {Text = "Test2"});
                await post.Apply(p => UpdateQuery(p)).Execute();
                Assert.Equal("Test2",
                    await new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Query<string>()).Single());
            }
            {
                var rowsNumber = await new {PostId = id}.Apply(p =>
                    DeleteByKeyQuery(typeof(Post), p)).Execute();
                Assert.Equal(1, rowsNumber);
                Assert.Empty(
                    await new {id}.Apply(p => new SqlCommand("SELECT Text FROM Post WHERE PostId = @Id").AddParams(p)
                        .Query<string>()).Read());
            }
        }

        private static void FillPost(Post post, PostData postData)
        {
            post.Text = postData.Text;
        }
        
        private static async Task Pagging(DateTime? date, int offset, int pageSize)
        {
            var paggingInfo = new {date, offset, pageSize}.ApplyDynamic(p => PagedQueries<Post>(
                query: (builder, command) =>
                {
                    builder.Append(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                    if (p.date.HasValue)
                        builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                },
                orderBy: (builder, command) => builder.Append("CreationDate DESC, PostId"),
                p.offset, p.pageSize));
            foreach (var record in await paggingInfo.Data.Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
            Console.WriteLine($"{await paggingInfo.Count.Read()}");
        }

        private static async Task ParentChildExample(int maxChildId = 10)
        {
            var queries = new {maxChildId}.Apply(p =>
            {
                var child = QueryAction((builder, command) => builder.Append(command, @"
SELECT *
FROM Child
WHERE ChildId <= @maxChildId", p));
                return new
                {
                    child = GetCommand(child).Query<Child>(),
                    parent = GetCommand((builder, command) => builder.Append($@"
SELECT *
FROM Parent
WHERE ParentId IN (SELECT ParentId FROM ({Text(child, command)}) T)")).Query<Parent>()
                };
            });
            var parentChild = (await queries.child.Read()).ToLookup(_ => _.ParentId);
            foreach (var parent in await queries.parent.Read())
            {
                Console.WriteLine(parent.ParentId);
                foreach (var child in parentChild[parent.ParentId])
                    Console.WriteLine($"  {child.ChildId}");
            }
        }

        private static async Task MyEnumExample()
        {
            var single = await new {a = MyEnum.A}
                .Apply(p => new SqlCommand(@"SELECT @a AS a").AddParams(p).Query<MyEnum?>()).Single();
            Assert.Equal(MyEnum.A, single);
        }

        private static async Task FuncExample()
        {
            var query = new {date = Func.New(() => DateTime.Now)}.ApplyDynamic(p => new SqlCommand(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE CreationDate > @date").AddParams(new {date = p.date()}).Query<Post>());
            foreach (var record in await query.Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task ChoiceExample(Choice<string, DateTime> textOrDate)
        {
            var query = new {textOrDate}.ApplyDynamic(p =>
            {
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
                return command.Query<Post>();
            });
            foreach (var record in await query.Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task ClusterExample(DateTime? startDate, DateTime? endDate)
        {
            var query = new
            {
                startDate = startDate.Cluster(),
                endDate = endDate.Cluster()
            }.ApplyDynamic(p =>
            {
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
                return command.Query<Post>();
            });
            foreach (var record in await query.Read())
            {
                Console.WriteLine($"{record.PostId} {record.Text} {record.CreationDate}");
            }
        }

        private static async Task DataReaderExample(DateTime? date)
        {
            await new {date}.ApplyDynamic(p =>
            {
                var command = new SqlCommand();
                var builder = new StringBuilder(@"
SELECT PostId, Text,  CreationDate
FROM Post
WHERE 1 = 1");
                if (p.date.HasValue)
                    builder.Append(command, @"
    AND CreationDate > @date", new {p.date});
                command.CommandText = builder.ToString();
                return command.Query(reader => reader.Read(() =>
                {
                    Console.WriteLine("{0} {1} {2}", reader.Int32(reader.Ordinal("PostId")),
                        reader.String(reader.Ordinal("Text")),
                        reader.DateTime(reader.Ordinal("CreationDate")));
                    return new { };
                }));
            }).Read();
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

    public class PostData
    {
        public string Text { get; set; }
    }
}