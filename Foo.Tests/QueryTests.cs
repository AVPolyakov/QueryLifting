using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            SqlUtil.QueryChecker = new QueryChecker();
        }

        [TestMethod]
        public void TestQueries()
        {
            foreach (var usage in new[] {typeof(Program)}.SelectMany(_ => _.Assembly.GetTypes()).ResolveUsages())
            {
                var methodInfo = usage.ResolveMember as MethodInfo;
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
                        invocation.Value(combination.ToArray());
                }
            }
        }

        private static IEnumerable<object> TestValues(ParameterInfo parameterInfo)
        {
            var type = parameterInfo.ParameterType;
            if (type == typeof (string)) return new[] {"test"};
            if (type == typeof (int)) return new object[] {0};
            if (type == typeof (decimal)) return new object[] {0m};
            if (type == typeof (Guid)) return new object[] {default(Guid)};
            if (type == typeof (DateTime)) return new object[] {new DateTime(2001, 1, 1)};
            if (type.IsEnum) return Enum.GetValues(type).Cast<object>();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>))
                return new[] {
                    GetMethodInfo<Func<int?>>(() => CreateNullable<int>()),
                    GetMethodInfo<Func<int, int?>>(_ => CreateNullable(_))
                }.SelectMany(prototypeMethod => {
                    var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    return method.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => method.Invoke(null, args.ToArray()));
                });
            if (type.IsAnonymousType())
                return type.GetConstructors(UsageResolver.AllBindingFlags)
                    .SelectMany(constructorInfo => constructorInfo.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => constructorInfo.Invoke(args.ToArray())));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Func<>))
            {
                var method = GetMethodInfo<Func<int, Func<int>>>(_ => CreateFunc(_))
                    .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                return method.GetParameters().GetAllCombinations(TestValues)
                    .Select(args => method.Invoke(null, args.ToArray()));
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Param<>))
            {
                var method = GetMethodInfo<Func<object, Param<object>>>(_ => CreateParam(_))
                    .GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                var args = method.GetParameters().Select(parameter => TestValues(parameter).First()).ToArray();
                return new[] {method.Invoke(null, args)};
            }
            throw new ApplicationException();
        }

        private static T? CreateNullable<T>(T arg) where T : struct
        {
            return arg;
        }

        private static T? CreateNullable<T>() where T : struct
        {
            return new T?();
        }

        private static Param<T> CreateParam<T>(T value)
        {
            return value.Param();
        }

        private static Func<T> CreateFunc<T>(T arg)
        {
            return () => arg;
        }

        private static readonly MethodInfo readMethod = GetMethodInfo<Func<SqlCommand, string, IEnumerable<object>>>(
            (command, connectionString) => command.Read<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo readMethod2 = GetMethodInfo<Func<SqlCommand, Func<SqlDataReader, object>, string, IEnumerable<object>>>(
            (command, materializer, connectionString) => command.Read(materializer, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod = GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<object>>>(
            (command, connectionString, func) => command.Query(func, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod2 = GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<IEnumerable<object>>>>(
            (command, connectionString, func) => command.Query<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo insertQueryMethod = GetMethodInfo<Func<object, string, object, Option<string>, Query<IEnumerable<object>>>>(
            (prototype, table, p, connectionString) => InsertQuery(prototype, table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo updateQueryMethod = GetMethodInfo<Func<string, object, Option<string>, NonQuery>>(
            (table, p, connectionString) => UpdateQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo deleteQueryMethod = GetMethodInfo<Func<string, object, Option<string>, NonQuery>>(
            (table, p, connectionString) => DeleteQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo nonQueryMethod = GetMethodInfo<Action<SqlCommand, string>>(
            (command, connectionString) => command.NonQuery(connectionString));

        private static readonly MethodInfo pagedQueriesMethod = typeof(FooSqlUtil).GetMethod(nameof(PagedQueries));

        private static readonly MethodInfo pagedQueryMethod = typeof(FooSqlUtil).GetMethod(nameof(PagedQuery));

        [TestMethod]
        public void ExplicitTest()
        {
            var methodInfo = typeof (Program).GetMethod(nameof(ReadPosts));
            foreach (var combination in methodInfo.GetParameters().GetAllCombinations(parameterInfo => {
                if (parameterInfo.Name == "date") return new object[] {new DateTime?(), new DateTime(2001, 1, 1),};
                throw new ApplicationException();
            }))
            {
                methodInfo.Invoke(null, combination.ToArray());
            }
        }
    }
}