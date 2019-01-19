using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using QueryLifting;
using static QueryLifting.SqlUtil;

namespace Foo
{
    public static class FooSqlUtil
    {
        public static SqlParameter AddParam(this SqlCommand command, string parameterName, MyEnum value) 
            => command.AddParam(parameterName, (int) value);

        public static MyEnum? NullableMyEnum(this SqlDataReader reader, int ordinal)
            => QueryChecker != null
                ? QueryChecker.Check<MyEnum?>(reader, ordinal)
                : (reader.IsDBNull(ordinal) ? new MyEnum?() : (MyEnum) reader.GetInt32(ordinal));

        public static PaggingInfo<IEnumerable<TData>, Query<int>> PagedQueries<TData>(
            Action<StringBuilder, SqlCommand> query, Action<StringBuilder, SqlCommand> orderBy, int offset, int pageSize,
            [CallerLineNumber] int line = 0, [CallerFilePath] string filePath = "")
        {
            return PaggingInfo.Create(GetCommand((builder, command) => builder.Append(command, $@"
{Text(query, command)}
ORDER BY
{Text(orderBy, command)}
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", new {offset, pageSize})).Read<TData>(line: line, filePath: filePath),
                GetCommand((builder, command) => builder.Append($@"
SELECT COUNT(*) FROM ({Text(query, command)}) T")).Query(reader => {
                    var enumerable = reader.Read<int?>().Select(_ => _.Value);
                    return QueryChecker == null ? enumerable.Single() : 0;
                }, line: line, filePath: filePath));
        }

        public static Query<PaggingInfo<List<TData>, int>> PagedQuery<TData>(
            Action<StringBuilder, SqlCommand> query, Action<StringBuilder, SqlCommand> orderBy, int offset, int pageSize,
            [CallerLineNumber] int line = 0, [CallerFilePath] string filePath = "")
        {
            return GetCommand((builder, command) => {
                var queryText = Text(query, command).ToString();
                builder.Append(command, $@"
{queryText}
ORDER BY
{Text(orderBy, command)}
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
SELECT COUNT(*) FROM ({queryText}) T;",
                    new {offset, pageSize});
            }).Query(reader => PaggingInfo.Create(
                reader.Read<TData>().ToList(), 
                QueryChecker == null ? reader.ReadNext<int?>().Select(_ => _.Value).Single() : 0),
                line: line, filePath: filePath);
        }

        public static T Transaction<T>(IsolationLevel isolationLevel, Func<SqlTransaction, T> func)
        {
            using (var connection = new SqlConnection(Program.ConnectionString))
            {
                connection.Open();
                return func(connection.BeginTransaction(isolationLevel));
            }
        }

        public static IEnumerable<T> Read<T>(this Query<IEnumerable<T>> query, SqlTransaction transaction)
        {
            query.Command.Connection = transaction.Connection;
            query.Command.Transaction = transaction;
            using (var reader = query.Command.ExecuteReader())
                foreach (var item in query.ReaderFunc(reader)) yield return item;
        }
    }
}