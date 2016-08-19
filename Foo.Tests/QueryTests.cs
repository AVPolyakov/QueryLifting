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
        }

        [TestMethod]
        public void TestQueries()
        {
            UsingQueryChecker(new QueryChecker(), () => {
                foreach (var usage in new[] {typeof (Program)}.SelectMany(_ => _.Assembly.GetTypes()).ResolveUsages())
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
                            invocation.Value(combination.ToArray());
                    }
                }
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
            });
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
            var original = SqlUtil.QueryChecker;
            SqlUtil.QueryChecker = queryChecker;
            try
            {
                action();
            }
            finally
            {
                SqlUtil.QueryChecker = original;
            }
        }
    }
}