using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using QueryLifting;
using static QueryLifting.SqlHelper;

namespace Foo
{
    public static class FooSqlHelper
    {
        public static PaggingInfo<Query<List<TData>>, Query<int>> PagedQueries<TData>(
            Action<StringBuilder, SqlCommand> query, Action<StringBuilder, SqlCommand> orderBy, int offset, int pageSize,
            [CallerLineNumber] int line = 0, [CallerFilePath] string filePath = "")
        {
            return PaggingInfo.Create(GetCommand((builder, command) => builder.Append(command, $@"
{Text(query, command)}
ORDER BY
{Text(orderBy, command)}
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", new {offset, pageSize})).Query<TData>(line: line, filePath: filePath),
                GetCommand((builder, command) => builder.Append($@"
SELECT COUNT(*) FROM ({Text(query, command)}) T")).Query(async reader => {
                    var enumerable = (await reader.Read<int?>()).Select(_ => _.Value);
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
            }).Query(async reader => {
                    var data = await reader.Read<TData>();
                    await reader.NextResultAsync();
                    return PaggingInfo.Create(
                        data,
                        QueryChecker == null ? (await reader.Read<int?>()).Select(_ => _.Value).Single() : 0);
                },
                line: line, filePath: filePath);
        }

        public static async Task<T> Transaction<T>(IsolationLevel isolationLevel, Func<SqlTransaction, Task<T>> func)
        {
            using (var connection = new SqlConnection(Program.ConnectionString))
            {
                await connection.OpenAsync();
                return await func(connection.BeginTransaction(isolationLevel));
            }
        }

        public static async Task<List<T>> Read<T>(this Query<List<T>> query, SqlTransaction transaction)
        {
            query.Command.Connection = transaction.Connection;
            query.Command.Transaction = transaction;
            using (var reader = await query.Command.ExecuteReaderAsync())
                return await query.ReaderFunc(reader);
        }
    }
}