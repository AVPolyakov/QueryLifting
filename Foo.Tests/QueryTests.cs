using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QueryLifting;
using static Foo.Program;

namespace Foo.Tests
{
    [TestClass]
    public class QueryTests
    {
        [TestInitialize]
        public void OnStartup()
        {
            SqlUtil.ConnectionStringFunc = () => ConnectionString;
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
                        readMethod, queryMethod, queryMethod2, insertQueryMethod, updateQueryMethod, deleteQueryMethod
                    }.Contains(methodInfo.GetGenericMethodDefinition()) ||
                     methodInfo == nonQueryMethod))
                {
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
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>))
                return new[] {
                    SqlUtil.GetMethodInfo<Func<int?>>(() => CreateNullable<int>()),
                    SqlUtil.GetMethodInfo<Func<int, int?>>(_ => CreateNullable(_))
                }.SelectMany(prototypeMethod => {
                    var method = prototypeMethod.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments());
                    return method.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => method.Invoke(null, args.ToArray()));
                });
            if (type.IsAnonymousType())
                return type.GetConstructors(UsageResolver.AllBindingFlags)
                    .SelectMany(constructorInfo => constructorInfo.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => constructorInfo.Invoke(args.ToArray())));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Param<>))
            {
                var method = SqlUtil.GetMethodInfo<Func<object, Param<object>>>(_ => CreateParam(_))
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

        private static readonly MethodInfo readMethod = SqlUtil.GetMethodInfo<Func<SqlCommand, string, IEnumerable<object>>>(
            (command, connectionString) => command.Read<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod = SqlUtil.GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<object>>>(
            (command, connectionString, func) => command.Query(func, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod2 = SqlUtil.GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<IEnumerable<object>>>>(
            (command, connectionString, func) => command.Query<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo insertQueryMethod = SqlUtil.GetMethodInfo<Func<object, string, object, Option<string>, Query<IEnumerable<object>>>>(
            (prototype, table, p, connectionString) => SqlUtil.InsertQuery(prototype, table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo updateQueryMethod = SqlUtil.GetMethodInfo<Func<string, object, Option<string>, NonQuery>>(
            (table, p, connectionString) => SqlUtil.UpdateQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo deleteQueryMethod = SqlUtil.GetMethodInfo<Func<string, object, Option<string>, NonQuery>>(
            (table, p, connectionString) => SqlUtil.DeleteQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo nonQueryMethod = SqlUtil.GetMethodInfo<Action<SqlCommand, string>>(
            (command, connectionString) => command.NonQuery(connectionString));

        [TestMethod]
        public void ExplicitTest()
        {
            TestQuery(typeof (Program), nameof(ReadPosts), parameterInfo => {
                if (parameterInfo.Name == "date") return new object[] {new DateTime?(), new DateTime(2001, 1, 1),};
                throw new ApplicationException();
            });
        }

        private static void TestQuery(Type type, string methodName, Func<ParameterInfo, IEnumerable<object>> testValues)
        {
            var methodInfo = type.GetMethod(methodName);
            foreach (var combination in methodInfo.GetParameters().GetAllCombinations(testValues))
                methodInfo.Invoke(null, combination.ToArray());
        }
    }
}