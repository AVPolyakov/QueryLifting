using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QueryLifting;

namespace Foo.Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void CheckQueries()
        {
            SqlUtil.ConnectionStringFunc = () => Program.ConnectionString;
            SqlUtil.QueryChecker = new QueryChecker();
            foreach (var usage in new[] {typeof(Program)}.SelectMany(_ => _.Assembly.GetTypes()).ResolveUsages())
            {
                var methodInfo = usage.ResolveMember as MethodInfo;
                if (methodInfo != null &&
                    (methodInfo.IsGenericMethod && new[] {
                        readMethod, queryMethod, queryMethod2, insertQueryMethod, updateQueryMethod, deleteQueryMethod
                    }.Contains(methodInfo.GetGenericMethodDefinition()) ||
                     methodInfo == nonQueryMethod))
                {
                    var currentMethod = usage.CurrentMethod as MethodInfo;
                    if (currentMethod != null && currentMethod.IsGenericMethod
                        && new[] {readMethod, queryMethod, queryMethod2, insertQueryMethod, updateQueryMethod, deleteQueryMethod}
                            .Contains(currentMethod.GetGenericMethodDefinition()))
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
            if (type == typeof (int)) return new[] {0}.Select<int, object>(_ => _);
            if (type == typeof (decimal)) return new[] {0m}.Select<decimal, object>(_ => _);
            if (type == typeof (DateTime)) return new[] {new DateTime(2001, 1, 1)}.Select<DateTime, object>(_ => _);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>))
                return new[] {
                    SqlUtil.GetMethodInfo<Func<int?>>(() => CreateNullable<int>()),
                    SqlUtil.GetMethodInfo<Func<int, int?>>(_ => CreateNullable(_))
                }.SelectMany(
                    method => method.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments())
                        .GetParameters().GetAllCombinations(TestValues)
                        .Select(args => method.GetGenericMethodDefinition().MakeGenericMethod(type.GetGenericArguments()).Invoke(null, args.ToArray())));
            if (type.IsAnonymousType())
                return type.GetConstructors(UsageResolver.AllBindingFlags)
                    .SelectMany(constructorInfo => constructorInfo.GetParameters().GetAllCombinations(TestValues)
                        .Select(args => constructorInfo.Invoke(args.ToArray())));
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

        private static readonly MethodInfo readMethod = SqlUtil.GetMethodInfo<Func<SqlCommand, string, IEnumerable<object>>>(
            (command, connectionString) => command.Read<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod = SqlUtil.GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<object>>>(
            (command, connectionString, func) => command.Query(func, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo queryMethod2 = SqlUtil.GetMethodInfo<Func<SqlCommand, string, Func<SqlDataReader, object>, Query<IEnumerable<object>>>>(
            (command, connectionString, func) => command.Query<object>(connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo insertQueryMethod = SqlUtil.GetMethodInfo<Func<string, object, Option<string>, Query<IEnumerable<int>>>>(
            (table, p, connectionString) => SqlUtil.InsertQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo updateQueryMethod = SqlUtil.GetMethodInfo<Func<string, object, Option<string>, NonQuery>>(
            (table, p, connectionString) => SqlUtil.UpdateQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo deleteQueryMethod = SqlUtil.GetMethodInfo<Func<string, object, Option<string>, NonQuery>>(
            (table, p, connectionString) => SqlUtil.DeleteQuery(table, p, connectionString)).GetGenericMethodDefinition();

        private static readonly MethodInfo nonQueryMethod = SqlUtil.GetMethodInfo<Action<SqlCommand, string>>(
            (command, connectionString) => command.NonQuery(connectionString));
    }
}