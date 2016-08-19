using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using QueryLifting;

namespace Foo
{
    public static class FooSqlUtil
    {
        public static SqlParameter AddParam(this SqlCommand command, string parameterName, MyEnum value) 
            => command.AddParam(parameterName, (int) value);

        public static Option<MyEnum> OptionMyEnum(this SqlDataReader reader, int ordinal)
            => SqlUtil.QueryChecker != null
                ? SqlUtil.QueryChecker.Check<Option<MyEnum>>(reader, ordinal)
                : (reader.IsDBNull(ordinal) ? new Option<MyEnum>() : (MyEnum) reader.GetInt32(ordinal));

        public static PaggingInfo<IEnumerable<TData>, Query<int>> PagedQueries<TData>(
            Action<StringBuilder, SqlCommand> query, Action<StringBuilder, SqlCommand> orderBy, int offset, int pageSize)
        {
            var command = new SqlCommand();
            {
                var builder = new StringBuilder();
                query(builder, command);
                builder.Append(@"
ORDER BY
");
                orderBy(builder, command);
                builder.Append(command, @"
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", new {offset, pageSize});
                command.CommandText = builder.ToString();
            }
            var countCommand = new SqlCommand();
            {
                var builder = new StringBuilder(@"
SELECT COUNT(*)
FROM (
");
                query(builder, countCommand);
                builder.Append(@"
) T1");
                countCommand.CommandText = builder.ToString();
            }
            return PaggingInfo.Create(command.Read<TData>(), 
                countCommand.Query(reader => {
                    var enumerable = reader.Read<Option<int>>().Select(_ => _.Value);
                    return SqlUtil.QueryChecker == null ? enumerable.Single() : 0;
                }));
        }

        public static Query<PaggingInfo<List<TData>, int>> PagedQuery<TData>(
            Action<StringBuilder, SqlCommand> query, Action<StringBuilder, SqlCommand> orderBy, int offset, int pageSize)
        {
            var command = new SqlCommand();
            string queryString;
            {
                var builder = new StringBuilder();
                query(builder, command);
                queryString = builder.ToString();
            }
            {
                var builder = new StringBuilder($@"{queryString}
ORDER BY
");
                orderBy(builder, command);
                builder.Append(command, $@"
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
SELECT COUNT(*)
FROM ({queryString}
) T1;",
                    new {offset, pageSize});
                command.CommandText = builder.ToString();
            }
            return command.Query(reader => {
                var data = reader.Read<TData>().ToList();
                reader.NextResult();
                var enumerable = reader.Read<Option<int>>().Select(_ => _.Value);
                return PaggingInfo.Create(data, SqlUtil.QueryChecker == null ? enumerable.Single() : 0);
            });
        }
    }
}