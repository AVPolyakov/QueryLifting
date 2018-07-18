using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Drawing;
using QueryLifting;
using static Foo.FooSqlUtil;
using static QueryLifting.SqlUtil;
using static Foo.Program;

namespace Foo.Tests
{
    [TestClass]
    public class QueryTests
    {
        [AssemblyInitialize]
        public static void OnStartup(TestContext context)
        {
            Init();
        }

        [TestMethod]
        public void TestQueries()
        {
            TestQueries(delegate { }, delegate { });
        }

        private void TestQueries(Action<SqlCommand, Option<string>> onQuery, Action<MethodBase> methodSetter)
        {
            UsingQueryChecker(new QueryChecker(onQuery), () => {
                foreach (var usage in new[] {typeof(Program)}.SelectMany(_ => _.Assembly.GetTypes()).ResolveUsages())
                {
                    var methodInfo = usage.ResolvedMember as MethodInfo;
                    if (methodInfo != null &&
                        (methodInfo.IsGenericMethod && new[] {
                             readMethod, readMethod2, queryMethod, queryMethod2, insertQueryMethod, updateQueryMethod,
                             deleteQueryMethod, pagedQueriesMethod, pagedQueryMethod
                         }.Contains(methodInfo.GetGenericMethodDefinition()) ||
                         methodInfo == nonQueryMethod))
                    {
                        var currentMethod = usage.CurrentMethod as MethodInfo;
                        if (currentMethod != null && currentMethod.IsGenericMethod && new[] {
                                pagedQueriesMethod, pagedQueryMethod
                            }.Contains(currentMethod.GetGenericMethodDefinition()))
                            continue;
                        var invocation = usage.CurrentMethod.GetStaticInvocation();
                        if (!invocation.HasValue) throw new ApplicationException();
                        foreach (var combination in usage.CurrentMethod.GetParameters().GetAllCombinations(TestValues))
                            UsingMethod(methodSetter, usage.CurrentMethod,
                                () => invocation.Value(combination.ToArray()));
                    }
                }
                TestMethod(typeof(Program).GetMethod(nameof(ReadPosts)), parameterInfo => {
                    if (parameterInfo.Name == "date") return new object[] {new DateTime?(), new DateTime(2001, 1, 1),};
                    throw new ApplicationException();
                }, methodSetter);
            });
        }

        private static void TestMethod(MethodInfo methodInfo, Func<ParameterInfo, IEnumerable<object>> choiceFunc, Action<MethodBase> methodSetter)
        {
            foreach (var combination in methodInfo.GetParameters().GetAllCombinations(choiceFunc))
                UsingMethod(methodSetter, methodInfo,
                    () => methodInfo.Invoke(null, combination.ToArray()));
        }

        private static IEnumerable<object> TestValues(ParameterInfo parameterInfo)
        {
            var type = parameterInfo.ParameterType;
            if (type == typeof (string)) return new[] {"test"};
            if (type == typeof (int)) return new object[] {0};
            if (type == typeof (decimal)) return new object[] {0m};
            if (type == typeof (Guid)) return new object[] {default(Guid)};
            if (type == typeof (DateTime)) return new object[] {new DateTime(2001, 1, 1)};
            if (type == typeof (bool)) return new object[] {true, false};
            if (type.IsEnum) return Enum.GetValues(type).Cast<object>();
            if (type.IsAnonymousType())
                return type.GetConstructors(UsageResolver.AllBindingFlags)
                    .SelectMany(constructorInfo => constructorInfo.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => constructorInfo.Invoke(args.ToArray())));
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof (Nullable<>))
                    return new[] {
                        GetMethodInfo<Func<int?>>(() => CreateNullable<int>()),
                        GetMethodInfo<Func<int, int?>>(_ => CreateNullable(_))
                    }.SelectMany(prototypeMethod => {
                        var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                        return method.GetParameters().GetAllCombinations(TestValues)
                            .Select(args => method.Invoke(null, args.ToArray()));
                    });
                if (genericType == typeof (Option<>))
                    return new[] {
                        GetMethodInfo<Func<Option<int>>>(() => CreateOption<int>()),
                        GetMethodInfo<Func<int, Option<int>>>(_ => CreateOption(_))
                    }.SelectMany(prototypeMethod => {
                        var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                        return method.GetParameters().GetAllCombinations(TestValues)
                            .Select(args => method.Invoke(null, args.ToArray()));
                    });
                if (genericType == typeof (Choice<,>))
                    return new[] {
                        GetMethodInfo<Func<object, Choice<object, object>>>(_ => Choice1<object, object>(_)),
                        GetMethodInfo<Func<object, Choice<object, object>>>(_ => Choice2<object, object>(_))
                    }.SelectMany(prototypeMethod => {
                        var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                        return method.GetParameters().GetAllCombinations(TestValues)
                            .Select(args => method.Invoke(null, args.ToArray()));
                    });
                if (genericType == typeof (Func<>))
                {
                    var method = GetMethodInfo<Func<int, Func<int>>>(_ => CreateFunc(_))
                        .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    return method.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => method.Invoke(null, args.ToArray()));
                }
                if (genericType == typeof (Param<>))
                {
                    var method = GetMethodInfo<Func<object, Param<object>>>(_ => CreateParam(_))
                        .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    var args = method.GetParameters().Select(parameter => TestValues(parameter).First()).ToArray();
                    return new[] {method.Invoke(null, args)};
                }
            }
            throw new ApplicationException();
        }

        private static T? CreateNullable<T>(T arg) where T : struct => arg;

        private static T? CreateNullable<T>() where T : struct => new T?();

        private static Option<T> CreateOption<T>(T arg) => arg;

        private static Option<T> CreateOption<T>() => new Option<T>();

        private static Choice<T1, T2> Choice1<T1, T2>(T1 arg) => arg;

        private static Choice<T1, T2> Choice2<T1, T2>(T2 arg) => arg;

        private static Param<T> CreateParam<T>(T value) => value.Param();

        private static Func<T> CreateFunc<T>(T arg) => () => arg;

        private static readonly MethodInfo readMethod = GetMethodInfo<Func<SqlCommand, string, IEnumerable<object>>>(
            (command, connectionString) => command.Read<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo readMethod2 = GetMethodInfo<Func<SqlCommand, Func<SqlDataReader, object>, string, IEnumerable<object>>>(
            (command, materializer, connectionString) => command.Read(materializer, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod = GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<object>>>(
            (command, connectionString, func) => command.Query(func, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod2 = GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<IEnumerable<object>>>>(
            (command, connectionString, func) => command.Query<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo insertQueryMethod = typeof(SqlUtil).GetMethod(nameof(InsertQuery));

        private static readonly MethodInfo updateQueryMethod = typeof(SqlUtil).GetMethod(nameof(UpdateQuery));

        private static readonly MethodInfo deleteQueryMethod = typeof(SqlUtil).GetMethod(nameof(DeleteQuery));

        private static readonly MethodInfo nonQueryMethod = GetMethodInfo<Action<SqlCommand, string>>(
            (command, connectionString) => command.NonQuery(connectionString));

        private static readonly MethodInfo pagedQueriesMethod = typeof(FooSqlUtil).GetMethod(nameof(PagedQueries));

        private static readonly MethodInfo pagedQueryMethod = typeof(FooSqlUtil).GetMethod(nameof(PagedQuery));

        private static void UsingQueryChecker(IQueryChecker queryChecker, Action action)
        {
            SqlUtil.QueryChecker = queryChecker;
            try
            {
                action();
            }
            finally
            {
                SqlUtil.QueryChecker = null;
            }
        }

        private static void UsingMethod(Action<MethodBase> methodSetter, MethodBase method, Action action)
        {
            methodSetter(method);
            try
            {
                action();
            }
            finally
            {
                methodSetter(null);
            }
        }

        [TestMethod]
        public void FindUsages()
        {
            MethodBase currentMethod = null;
            var queries = new Dictionary<Tuple<string, int, int>, HashSet<Tuple<string, string>>>();
            TestQueries((command, connectionString) => {
                if (command.CommandType == CommandType.StoredProcedure) return;
                var stackTrace = new StackTrace(true);
                for (var i = 0; ; i++)
                {
                    var frame = stackTrace.GetFrame(i);
                    if (frame.GetMethod() == currentMethod)
                    {
                        var line = frame.GetFileLineNumber();
                        var column = frame.GetFileColumnNumber();
                        var file = frame.GetFileName();
                        var key = Tuple.Create(file, line, column);
                        HashSet<Tuple<string, string>> hashSet;
                        if (!queries.TryGetValue(key, out hashSet))
                        {
                            hashSet = new HashSet<Tuple<string, string>>();
                            queries.Add(key, hashSet);
                        }
                        var paramClause = string.Join(",", command.Parameters.Cast<SqlParameter>()
                            .Select(_ => $"{_.ParameterName} {GetSqlTypeString(_)}"));
                        hashSet.Add(Tuple.Create($@"
{paramClause}
AS 
    BEGIN
{command.CommandText}
    END",
                            connectionString.Match(_ => _, ConnectionStringFunc)));
                        break;
                    }
                }
            }, method => currentMethod = method);
            SharpLayout.Document.CollectCallerInfo = true;
            var document = new SharpLayout.Document();
            var settings = new SharpLayout.PageSettings();
            settings.LeftMargin = settings.TopMargin = settings.RightMargin = settings.BottomMargin = SharpLayout.Util.Cm(0.5);
            var section = document.Add(new SharpLayout.Section(settings));
            foreach (var usage in FindUsages(queries, tableName: "Post", columnName: "CreationDate"))
            {
                var file = Path.GetFileName(usage.Item1);
                section.Add(new SharpLayout.Paragraph()
                    .Add($"{file}, Ln {usage.Item2}, Col {usage.Item3}", new XFont("Consolas", 9.5),
                        line: usage.Item2, filePath: usage.Item1));
                Console.WriteLine($"{file}, Ln {usage.Item2}, Col {usage.Item3}");
            }
            StartLiveViewer(document.SavePng(0, "Temp.png", 120), true);
        }

        public static void StartLiveViewer(string fileName, bool alwaysShowWindow, bool findId = true)
        {
            var processes = Process.GetProcessesByName("LiveViewer");
            if (processes.Length <= 0)
            {
                string arguments;
                if (findId && Ide == vs)
                {
                    const string solutionName = "QueryLifting";
                    var firstOrDefault = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == "devenv" &&
                        p.MainWindowTitle.Contains(solutionName));
                    if (firstOrDefault != null)
                        arguments = $" {firstOrDefault.Id}";
                    else
                        arguments = "";
                }
                else
                    arguments = "";
                Process.Start("LiveViewer", fileName + " " + Ide + arguments);
            }
            else
            {
                if (alwaysShowWindow)
                    SetForegroundWindow(processes[0].MainWindowHandle);
            }
        }

        private static string Ide => vs;

        private const string vs = "vs";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static IEnumerable<Tuple<string, int, int>> FindUsages(
            Dictionary<Tuple<string, int, int>, HashSet<Tuple<string, string>>> queries,
            string tableName, Option<string> columnName = new Option<string>())
        {
            var prefix = Guid.NewGuid().ToString("N");
            foreach (var query in queries)
                foreach (var tuple in query.Value)
                {
                    object result;
                    using (var connection = new SqlConnection(tuple.Item2))
                    {
                        connection.Open();
                        DropProcedure(connection, prefix);
                        using (var command = new SqlCommand())
                        {
                            command.Connection = connection;
                            command.CommandText = $@"
CREATE PROCEDURE {QueryLiftingTemp}{prefix}
{tuple.Item1}";
                            command.ExecuteNonQuery();
                        }
                        using (var command = new SqlCommand())
                        {
                            command.Connection = connection;
                            var builder = new StringBuilder();
                            builder.AppendFormat($@"
SELECT  1
FROM    sys.dm_sql_referenced_entities('dbo.{QueryLiftingTemp}{prefix}', 'OBJECT')
WHERE   referenced_entity_name = @referenced_entity_name");
                            if (columnName.HasValue)
                            {
                                builder.Append(@"
        AND referenced_minor_name = @referenced_minor_name");
                                command.Parameters.Add(new SqlParameter {
                                    SqlDbType = SqlDbType.NVarChar,
                                    Size = -1,
                                    ParameterName = "@referenced_minor_name",
                                    Value = columnName.Value
                                });
                            }
                            else builder.Append(@"
        AND referenced_minor_name IS NULL");
                            command.Parameters.Add(new SqlParameter {
                                SqlDbType = SqlDbType.NVarChar,
                                Size = -1,
                                ParameterName = "@referenced_entity_name",
                                Value = tableName
                            });
                            command.CommandText = builder.ToString();
                            result = command.ExecuteScalar();
                        }
                        DropProcedure(connection, prefix);
                    }
                    if (result != null)
                    {
                        yield return query.Key;
                        break;
                    }
                }
        }

        private static string GetSqlTypeString(SqlParameter parameter)
        {
            switch (parameter.SqlDbType)
            {
                case SqlDbType.UniqueIdentifier:
                    return "UNIQUEIDENTIFIER";
                case SqlDbType.Int:
                    return "INT";
                case SqlDbType.NVarChar:
                    if (parameter.Size == -1)
                        return "NVARCHAR(MAX)";
                    else
                        throw new ApplicationException();
                case SqlDbType.DateTime:
                    return "DATETIME";
                case SqlDbType.Decimal:
                    return "DECIMAL";
                default:
                    throw new ApplicationException();
            }
        }

        private static void DropProcedure(SqlConnection connection, string prefix)
        {
            using (var command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = $@"
IF EXISTS ( SELECT  *
            FROM    sys.objects
            WHERE   object_id = OBJECT_ID(N'[dbo].[{QueryLiftingTemp}{prefix}]')
                    AND type IN ( N'P', N'PC' ) ) 
    DROP PROCEDURE [dbo].[{QueryLiftingTemp}{prefix}]";
                command.ExecuteNonQuery();
            }
        }

        private const string QueryLiftingTemp = "QueryLiftingTemp";
    }
}