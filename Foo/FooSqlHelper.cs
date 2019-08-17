using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
SELECT COUNT(*) FROM ({Text(query, command)}) T")).Query(async reader =>
                {
                    var enumerable = (await reader.Read<int?>()).Select(_ => _.Value);
                    return QueryChecker == null ? enumerable.Single() : 0;
                }, line: line, filePath: filePath));
        }
    }
}