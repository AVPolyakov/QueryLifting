using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using QueryLifting;

namespace Foo.Tests
{
    internal class QueryChecker : IQueryChecker
    {
        private readonly Action<QueryInfo> onQuery;
        private readonly Action<Task> setTask;

        public QueryChecker(Action<QueryInfo> onQuery, Action<Task> setTask)
        {
            this.onQuery = onQuery;
            this.setTask = setTask;
        }

        public void Query<T>(Query<T> query)
        {
            var info = new QueryInfo(query.Command, query.ConnectionString, query.Line, query.FilePath);
            onQuery(info);
            try
            {
                using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlHelper.ConnectionStringFunc)))
                {
                    query.Command.Connection = connection;
                    connection.Open();
                    using (var reader = query.Command.ExecuteReader(CommandBehavior.SchemaOnly))
                    {
                        var task = query.ReaderFunc(reader);
                        async Task ToVoidTask() => await task;
                        setTask(ToVoidTask());
                    }
                }
            }
            catch (Exception e)
            {
                throw GetException(e, info);
            }
        }


        private static QueryCheckException GetException(Exception e, QueryInfo info)
        {
            if (e is QueryCheckException checkException && checkException.QueryResultType.HasValue)
                return new QueryCheckException($@"{e.Message}{(e.Message.EndsWith(".") ? "" : ".")} Information about query File and line: {info.FilePath}:line {info.Line}, Query text: {info.Command.CommandText},
Query result type:
{checkException.QueryResultType.Value}", e);
            else
                return new QueryCheckException($"{e.Message}{(e.Message.EndsWith(".") ? "" : ".")} Information about query File and line: {info.FilePath}:line {info.Line}, Query text: {info.Command.CommandText}", e);
        }

        public Task<List<T>> Read<T>(SqlDataReader reader, Func<T> materializer)
        {
            var ordinals = new HashSet<int>();
            if (!ordinalDictionary.TryAdd(reader, ordinals)) throw new Exception();
            try
            {
                materializer();
            }
            finally
            {
	            if (!ordinalDictionary.TryRemove(reader, out _)) throw new Exception();
            }
            if (ordinals.Count != reader.FieldCount)
                throw new QueryCheckException("Field count mismatch", queryResultType: GetQueryResultType(reader));
            return Task.FromResult(new List<T>());
        }

        public T Check<T>(SqlDataReader reader, int ordinal)
        {
	        if (!ordinalDictionary.TryGetValue(reader, out var ordinals)) throw new Exception();
            ordinals.Add(ordinal);
            var type = typeof (T);
            QueryCheckException GetInnerException() => new QueryCheckException($"Type mismatch for field '{reader.GetName(ordinal)}', type in query {reader.GetFieldType(ordinal)}, type in result {type}",
                queryResultType: GetQueryResultType(reader));
            if (AllowDBNull(reader, ordinal))
            {
	            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
		            TypesAreCompatible(reader.GetFieldType(ordinal), type.GetGenericArguments().Single()))
	            {
		            //no-op
	            }
	            else if (type == typeof(string) && TypesAreCompatible(reader.GetFieldType(ordinal), type))
                {
                    //no-op
                }
                else
                    throw GetInnerException();
            }
            else
            {
                if (!TypesAreCompatible(reader.GetFieldType(ordinal), type))
                    throw GetInnerException();
            }
            return default(T);
        }

        public int GetOrdinal(SqlDataReader reader, string name)
        {
            try
            {
                return reader.GetOrdinal(name);
            }
            catch (IndexOutOfRangeException)
            {
                throw new QueryCheckException($"Field '{name}' not found in query",
                    queryResultType: GetQueryResultType(reader));
            }
        }

        private static bool TypesAreCompatible(Type dbType, Type type)
        {
            if (type.IsEnum && dbType == Enum.GetUnderlyingType(type)) return true;
            return type == dbType;
        }

        private static string GetQueryResultType(SqlDataReader reader)
        {
            return string.Join(Environment.NewLine,
                Enumerable.Range(0, reader.FieldCount).Select(i => {
                    var type = AllowDBNull(reader, i) && reader.GetFieldType(i).IsValueType 
                        ? typeof(Nullable<>).MakeGenericType(reader.GetFieldType(i)) 
                        : reader.GetFieldType(i);
                    var typeName = type.GetCSharpName();
                    return $"        public {typeName} {reader.GetName(i)} {{ get; set; }}";
                }));
        }

        private static bool AllowDBNull(SqlDataReader reader, int ordinal)
        {
            return (bool)reader.GetSchemaTable().Rows[ordinal]["AllowDBNull"];
        }

        private readonly ConcurrentDictionary<SqlDataReader, HashSet<int>> ordinalDictionary = new ConcurrentDictionary<SqlDataReader, HashSet<int>>();

        public void NonQuery(NonQuery query)
        {
            QueryCheckException GetInnerException() => new QueryCheckException("Parameter type mismatch");
            var info = new QueryInfo(query.Command, query.ConnectionString, query.Line, query.FilePath);
            onQuery(info);
            try
            {
                using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlHelper.ConnectionStringFunc)))
                    if (query.Command.CommandType == CommandType.StoredProcedure)
                    {
                        var command = new SqlCommand(query.Command.CommandText, connection) {CommandType = CommandType.StoredProcedure};
                        connection.Open();
                        SqlCommandBuilder.DeriveParameters(command);
                        var dictionary = command.Parameters.Cast<SqlParameter>().ToDictionary(_ => _.ParameterName);
                        foreach (SqlParameter parameter in query.Command.Parameters)
                        {
                            if (dictionary.TryGetValue(parameter.ParameterName, out var value))
                            {
                                if (parameter.SqlDbType != value.SqlDbType)
                                    if (parameter.SqlDbType == SqlDbType.NVarChar && value.SqlDbType == SqlDbType.VarChar)
                                    {
                                        //no-op
                                    }
                                    else
                                        throw GetInnerException();
                                if (value.Size == -1)
                                {
                                    if (parameter.Size != -1) throw GetInnerException();
                                }
                                else
                                {
                                    if (parameter.Size == -1)
                                    {
                                        //no-op
                                    }
                                    else
                                    {
                                        if (parameter.Size < value.Size) throw GetInnerException();
                                    }
                                }
                            }
                            else
                                throw GetInnerException();
                        }
                    }
                    else
                    {
                        query.Command.Connection = connection;
                        connection.Open();
                        using (query.Command.ExecuteReader(CommandBehavior.SchemaOnly))
                        {
                        }
                    }
            }
            catch (Exception e)
            {
                throw GetException(e, info);
            }
        }
    }

    public struct QueryInfo
    {
        public SqlCommand Command { get; }
        public Option<string> ConnectionString { get; }
        public int Line { get; }
        public string FilePath { get; }

        public QueryInfo(SqlCommand command, Option<string> connectionString, int line, string filePath)
        {
            Command = command;
            ConnectionString = connectionString;
            Line = line;
            FilePath = filePath;
        }
    }

    public class QueryCheckException : Exception
    {
        public Option<string> QueryResultType { get; }

        public QueryCheckException(string message, Exception innerException = null,
            Option<string> queryResultType = new Option<string>())
            : base(message, innerException)
        {
            QueryResultType = queryResultType;
        }
    }
}