using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using QueryLifting;

namespace Foo.Tests
{
    internal class QueryChecker : IQueryChecker
    {
        private readonly Action<QueryInfo> onQuery;

        public QueryChecker(Action<QueryInfo> onQuery)
        {
            this.onQuery = onQuery;
        }

        public void Query<T>(Query<T> query)
        {
            var info = new QueryInfo(query.Command, query.ConnectionString, query.Line, query.FilePath);
            onQuery(info);
            try
            {
                using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlUtil.ConnectionStringFunc)))
                {
                    query.Command.Connection = connection;
                    connection.Open();
                    using (var reader = query.Command.ExecuteReader(CommandBehavior.SchemaOnly))
                        query.ReaderFunc(reader);
                }
            }
            catch (Exception e)
            {
                throw GetException(e, info);
            }
        }

        private static ApplicationException GetException(Exception e, QueryInfo info)
        {
            return new ApplicationException($"{e.Message}{(e.Message.EndsWith(".") ? "" : ".") } Information about query Line: {info.Line}, File: {info.FilePath} QueryText: {info.Command.CommandText}", e);
        }

        public IEnumerable<T> Read<T>(SqlDataReader reader, Func<T> materializer)
        {
            var ordinals = new HashSet<int>();
            if (!ordinalDictionary.TryAdd(reader, ordinals)) throw new ApplicationException();
            try
            {
                materializer();
            }
            finally
            {
	            if (!ordinalDictionary.TryRemove(reader, out _)) throw new ApplicationException();
            }
            if (ordinals.Count != reader.FieldCount)
            {
                WriteDataRetrievingCode(reader);
                throw new ApplicationException("Field count mismatch");
            }
            return Enumerable.Empty<T>();
        }

        public T Check<T>(SqlDataReader reader, int ordinal)
        {
	        if (!ordinalDictionary.TryGetValue(reader, out var ordinals)) throw new ApplicationException();
            ordinals.Add(ordinal);
            var type = typeof (T);
            ApplicationException GetInnerException() => new ApplicationException($"Type mismatch for field '{reader.GetName(ordinal)}'");
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
	            {
		            WriteDataRetrievingCode(reader);
		            throw GetInnerException();
	            }
            }
            else
            {
                if (!TypesAreCompatible(reader.GetFieldType(ordinal), type))
                {
                    WriteDataRetrievingCode(reader);
                    throw GetInnerException();
                }
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
                WriteDataRetrievingCode(reader);
                throw new ApplicationException($"Field '{name}' not found in query");
            }
        }

        private static bool TypesAreCompatible(Type dbType, Type type)
        {
            if (dbType == typeof (int) && type == typeof (MyEnum)) return true;
            return type == dbType;
        }

        private static void WriteDataRetrievingCode(SqlDataReader reader)
        {
            Console.WriteLine(string.Join(Environment.NewLine,
                Enumerable.Range(0, reader.FieldCount).Select(i => {
                    var typeName = (AllowDBNull(reader, i) ? typeof (Option<>).MakeGenericType(reader.GetFieldType(i)) : reader.GetFieldType(i)).GetCSharpName();
                    return $"        public {typeName} {reader.GetName(i)} {{ get; set; }}";
                })));
        }

        private static bool AllowDBNull(SqlDataReader reader, int ordinal)
        {
            return (bool)reader.GetSchemaTable().Rows[ordinal]["AllowDBNull"];
        }

        private readonly ConcurrentDictionary<SqlDataReader, HashSet<int>> ordinalDictionary = new ConcurrentDictionary<SqlDataReader, HashSet<int>>();

        public void NonQuery(NonQuery query)
        {
	        ApplicationException GetInnerException() => new ApplicationException("Parameter type mismatch");
            var info = new QueryInfo(query.Command, query.ConnectionString, query.Line, query.FilePath);
            onQuery(info);
            try
            {

            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlUtil.ConnectionStringFunc)))
                if (query.Command.CommandType == CommandType.StoredProcedure)
                {
                    var command = new SqlCommand(query.Command.CommandText, connection) { CommandType = CommandType.StoredProcedure };
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
}