using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using QueryLifting;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable once RedundantUsingDirective
using static Examples.ExamplesSqlHelper;
using static QueryLifting.SqlHelper;
using static Examples.Program;

namespace Examples.Tests
{
    public class QueryTests
    {
        private readonly ITestOutputHelper outputHelper;

        public QueryTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        [Fact]
        public async Task TestQueries()
        {
            await IterateQueries(queryInfo => { });
        }

        [Fact]
        public async Task FirstOnly_TestQueries()
        {
            EnumerableExtensions.FirstOnly = true;
            try
            {
                await IterateQueries(queryInfo => { });
            }
            finally
            {
                EnumerableExtensions.FirstOnly = false;
            }
        }

        [Fact]
        public async Task FindUsagesTest()
        {
            var queries = new Dictionary<Tuple<string, int>, HashSet<Tuple<string, string>>>();
            await IterateQueries(queryInfo => AddQuery(queryInfo, queries));
            var entityName = "Post";
            var columnName = "CreationDate";
            var usages = string.Join(Environment.NewLine,
                FindUsages(queries, entityName: entityName, columnName: columnName)
                    .Select(_ => $"{_.Item1}:line {_.Item2}"));
            var result = $@"Search result for EntityName: {entityName}, ColumnName: {columnName}
{usages}
---end search result---";
            outputHelper.WriteLine(result);
        }

        public static IEnumerable<object> TestValues(ParameterInfo parameterInfo)
        {
            return TestValues(parameterInfo.ParameterType);
        }

        public static IEnumerable<object> TestValues(PropertyInfo propertyInfo)
        {
            return TestValues(propertyInfo.PropertyType);
        }

        private static readonly HashSet<Type> dataObjectTypes = new[]
        {
            typeof(Post),
        }.ToHashSet();
        
        public static IEnumerable<object> TestValues(Type type)
        {
            if (type == typeof(string)) return new[] {"test"};
            if (type == typeof(int)) return new object[] {0};
            if (type == typeof(decimal)) return new object[] {0m};
            if (type == typeof(Guid)) return new object[] {default(Guid)};
            if (type == typeof(DateTime)) return new object[] {new DateTime(2001, 1, 1)};
            if (type == typeof(bool)) return new object[] {true, false};
            if (type.IsEnum) return Enum.GetValues(type).Cast<object>();
            if (type.IsAnonymousType())
                return type.GetConstructors(UsageResolver.AllBindingFlags)
                    .SelectMany(constructorInfo => constructorInfo.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => constructorInfo.Invoke(args.ToArray())));
            if (dataObjectTypes.Contains(type))
                return GetInstancesByProperties(type);
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(Nullable<>))
                    return new[]
                    {
                        GetMethodInfo<Func<int?>>(() => CreateNullable<int>()),
                        GetMethodInfo<Func<int, int?>>(_ => CreateNullable(_))
                    }.SelectMany(prototypeMethod =>
                    {
                        var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                        return method.GetParameters().GetAllCombinations(TestValues)
                            .Select(args => method.Invoke(null, args.ToArray()));
                    });
                if (genericType == typeof(Option<>))
                    return new[]
                    {
                        GetMethodInfo<Func<Option<int>>>(() => CreateOption<int>()),
                        GetMethodInfo<Func<int, Option<int>>>(_ => CreateOption(_))
                    }.SelectMany(prototypeMethod =>
                    {
                        var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                        return method.GetParameters().GetAllCombinations(TestValues)
                            .Select(args => method.Invoke(null, args.ToArray()));
                    });
                if (genericType == typeof(Choice<,>))
                    return new[]
                    {
                        GetMethodInfo<Func<object, Choice<object, object>>>(_ => Choice1<object, object>(_)),
                        GetMethodInfo<Func<object, Choice<object, object>>>(_ => Choice2<object, object>(_))
                    }.SelectMany(prototypeMethod =>
                    {
                        var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                        return method.GetParameters().GetAllCombinations(TestValues)
                            .Select(args => method.Invoke(null, args.ToArray()));
                    });
                if (genericType == typeof(Func<>))
                {
                    var method = GetMethodInfo<Func<int, Func<int>>>(_ => CreateFunc(_))
                        .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    return method.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => method.Invoke(null, args.ToArray()));
                }
                if (genericType == typeof(Param<>))
                {
                    var method = GetMethodInfo<Func<object, Param<object>>>(_ => _.Param())
                        .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    var args = method.GetParameters().Select(parameter => TestValues(parameter).First()).ToArray();
                    return new[] {method.Invoke(null, args)};
                }
                if (genericType == typeof(Params<>))
                {
                    var method = GetMethodInfo<Func<object, Params<object>>>(_ => _.Params())
                        .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    var args = method.GetParameters().Select(parameter => TestValues(parameter).First()).ToArray();
                    return new[] {method.Invoke(null, args)};
                }
                if (genericType == typeof(Cluster<>))
                {
                    var method = GetMethodInfo<Func<object, Cluster<object>>>(_ => _.Cluster())
                        .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    return method.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => method.Invoke(null, args.ToArray()));
                }
            }
            throw new QueryCheckException($"Test value not found for parameter type `{type}`");
        }
        
        private async Task IterateQueries(Action<QueryInfo> onQuery)
        {
            Task task = null;
            SqlHelper.QueryChecker = new QueryChecker(onQuery, t => task = t);
            try
            {
                foreach (var usage in new[] {typeof(Program)}.SelectMany(_ => _.Assembly.GetTypes()).ResolveUsages())
                {
                    var methodInfo = usage.ResolvedMember as MethodInfo;
                    if (methodInfo != null &&
                        (methodInfo.IsGenericMethod && new[]
                            {
                                queryMethod, queryMethod2, insertQueryMethod, updateQueryMethod,
                                deleteQueryMethod, pagedQueriesMethod, getQueryMethod
                            }.Contains(methodInfo.GetGenericMethodDefinition()) ||
                            methodInfo == nonQueryMethod))
                    {
                        var currentMethod = usage.CurrentMethod as MethodInfo;
                        if (currentMethod != null && currentMethod.IsGenericMethod && new[]
                        {
                            pagedQueriesMethod
                        }.Contains(currentMethod.GetGenericMethodDefinition()))
                            continue;
                        var invocation = usage.CurrentMethod.GetStaticInvocation();
                        if (!invocation.HasValue) throw new QueryCheckException("Method must be static");
                        foreach (var combination in usage.CurrentMethod.GetParameters().GetAllCombinations(TestValues))
                        {
                            invocation.Value(combination.ToArray());
                            await task;
                        }
                    }
                }
            }
            finally
            {
                SqlHelper.QueryChecker = null;
            }
        }
        
        private static IEnumerable<object> GetInstancesByProperties(Type type)
        {
            return type.GetProperties()
                .GetAllCombinations(propertyInfo => TestValues(propertyInfo)
                    .Select(value => new {value, propertyInfo}))
                .Select(propertyValues =>
                {
                    var instance = type.GetConstructor(new Type[] { }).Invoke(new object[] { });
                    foreach (var propertyValue in propertyValues)
                        propertyValue.propertyInfo.SetMethod.Invoke(instance, new[] {propertyValue.value});
                    return instance;
                });
        }
        
        private static T? CreateNullable<T>(T arg) where T : struct => arg;

        private static T? CreateNullable<T>() where T : struct => new T?();

        private static Option<T> CreateOption<T>(T arg) => arg;

        private static Option<T> CreateOption<T>() => new Option<T>();

        private static Choice<T1, T2> Choice1<T1, T2>(T1 arg) => arg;

        private static Choice<T1, T2> Choice2<T1, T2>(T2 arg) => arg;

        private static Func<T> CreateFunc<T>(T arg) => () => arg;

        private static readonly MethodInfo queryMethod = GetMethodInfo<Func<SqlCommand, string, int, string, Func<SqlDataReader, Task<object>>, Query<object>>>(
            (command, connectionString, line, filePath, func) => command.Query(func, connectionString, line, filePath)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod2 = GetMethodInfo<Func<SqlCommand, string, int, string, Func<SqlDataReader, Task<object>>, Query<List<object>>>>(
            (command, connectionString, line, filePath, func) => command.Query<object>(connectionString, line, filePath)).GetGenericMethodDefinition();

        private static readonly MethodInfo insertQueryMethod = typeof(SqlHelper).GetMethod(nameof(InsertQuery));

        private static readonly MethodInfo updateQueryMethod = typeof(SqlHelper).GetMethod(nameof(UpdateQuery));

        private static readonly MethodInfo deleteQueryMethod = typeof(SqlHelper).GetMethod(nameof(DeleteByKeyQuery));

        private static readonly MethodInfo getQueryMethod = typeof(SqlHelper).GetMethod(nameof(GetByKeyQuery));

        private static readonly MethodInfo nonQueryMethod = GetMethodInfo<Action<SqlCommand, string, int, string>>(
            (command, connectionString, line, filePath) => command.NonQuery(connectionString, line, filePath));

        private static readonly MethodInfo pagedQueriesMethod = typeof(ExamplesSqlHelper).GetMethod(nameof(PagedQueries));
        
        private static IEnumerable<Tuple<string, int>> FindUsages(
            Dictionary<Tuple<string, int>, HashSet<Tuple<string, string>>> queries,
            string entityName, Option<string> columnName = new Option<string>())
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
                            command.Parameters.Add(new SqlParameter
                            {
                                SqlDbType = SqlDbType.NVarChar,
                                Size = -1,
                                ParameterName = "@referenced_minor_name",
                                Value = columnName.Value
                            });
                        }
                        else
                            builder.Append(@"
        AND referenced_minor_name IS NULL");
                        command.Parameters.Add(new SqlParameter
                        {
                            SqlDbType = SqlDbType.NVarChar,
                            Size = -1,
                            ParameterName = "@referenced_entity_name",
                            Value = entityName
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
                    if (parameter.Size < 0 || parameter.Size > DefaultLength)
                        return "NVARCHAR(MAX)";
                    else
                        return $"NVARCHAR({parameter.Size})";
                case SqlDbType.DateTime:
                    return "DATETIME";
                case SqlDbType.Decimal:
                    return $"DECIMAL({parameter.Precision}, {parameter.Scale})";
                default:
                    throw new Exception();
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

        private static void AddQuery(QueryInfo queryInfo, Dictionary<Tuple<string, int>, HashSet<Tuple<string, string>>> queries)
        {
            if (queryInfo.Command.CommandType == CommandType.StoredProcedure) return;
            {
                var line = queryInfo.Line;
                var file = queryInfo.FilePath;
                var key = Tuple.Create(file, line);
                if (!queries.TryGetValue(key, out var hashSet))
                {
                    hashSet = new HashSet<Tuple<string, string>>();
                    queries.Add(key, hashSet);
                }
                var paramClause = string.Join(",", queryInfo.Command.Parameters.Cast<SqlParameter>()
                    .Select(_ => $"{_.ParameterName} {GetSqlTypeString(_)}"));
                hashSet.Add(Tuple.Create($@"
{paramClause}
AS 
    BEGIN
{queryInfo.Command.CommandText}
    END",
                    queryInfo.ConnectionString.Match(_ => _, ConnectionStringFunc)));
            }
        }
        
        static QueryTests()
        {
            Init();
        }
    }
}