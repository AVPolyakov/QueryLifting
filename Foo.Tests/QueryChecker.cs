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
        private readonly Action<SqlCommand, Option<string>> onQuery;

        public QueryChecker(Action<SqlCommand, Option<string>> onQuery)
        {
            this.onQuery = onQuery;
        }

        public void Query<T>(Query<T> query)
        {
            onQuery(query.Command, query.ConnectionString);
            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlUtil.ConnectionStringFunc)))
            {
                query.Command.Connection = connection;
                connection.Open();
                using (var reader = query.Command.ExecuteReader(CommandBehavior.SchemaOnly))
                    query.ReaderFunc(reader);
            }
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
                HashSet<int> value;
                if (!ordinalDictionary.TryRemove(reader, out value)) throw new ApplicationException();
            }
            if (ordinals.Count != reader.FieldCount)
            {
                WriteDataRetrievingCode(reader);
                throw new ApplicationException();
            }
            return Enumerable.Empty<T>();
        }

        public T Check<T>(SqlDataReader reader, int ordinal)
        {
            HashSet<int> ordinals;
            if (!ordinalDictionary.TryGetValue(reader, out ordinals)) throw new ApplicationException();
            ordinals.Add(ordinal);
            var type = typeof (T);
            if (AllowDBNull(reader, ordinal))
            {
                if (!(type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Option<>) &&
                      TypesAreCompatible(reader.GetFieldType(ordinal), type.GetGenericArguments().Single())))
                {
                    WriteDataRetrievingCode(reader);
                    throw new ApplicationException();
                }
            }
            else
            {
                if (!TypesAreCompatible(reader.GetFieldType(ordinal), type))
                {
                    WriteDataRetrievingCode(reader);
                    throw new ApplicationException();
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
                throw;
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
            onQuery(query.Command, query.ConnectionString);
            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlUtil.ConnectionStringFunc)))
                if (query.Command.CommandType == CommandType.StoredProcedure)
                {
                    var command = new SqlCommand(query.Command.CommandText, connection) { CommandType = CommandType.StoredProcedure };
                    connection.Open();
                    SqlCommandBuilder.DeriveParameters(command);
                    var dictionary = command.Parameters.Cast<SqlParameter>().ToDictionary(_ => _.ParameterName);
                    foreach (SqlParameter parameter in query.Command.Parameters)
                    {
                        SqlParameter value;
                        if (dictionary.TryGetValue(parameter.ParameterName, out value))
                        {
                            if (parameter.SqlDbType != value.SqlDbType)
                                if (parameter.SqlDbType == SqlDbType.NVarChar && value.SqlDbType == SqlDbType.VarChar)
                                {
                                    //no-op
                                }
                                else
                                    throw new ApplicationException();
                            if (value.Size == -1)
                            {
                                if (parameter.Size != -1) throw new ApplicationException();
                            }
                            else
                            {
                                if (parameter.Size == -1)
                                {
                                    //no-op
                                }
                                else
                                {
                                    if (parameter.Size < value.Size) throw new ApplicationException();
                                }
                            }
                        }
                        else
                            throw new ApplicationException();
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
    }
}