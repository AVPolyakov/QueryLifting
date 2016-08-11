using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using QueryLifting;

namespace Foo.Tests
{
    internal class QueryChecker : IQueryChecker
    {
        public void Query<T>(Query<T> query)
        {
            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, SqlUtil.ConnectionStringFunc)))
            {
                query.Command.Connection = connection;
                connection.Open();
                using (var reader = query.Command.ExecuteReader(CommandBehavior.SchemaOnly))
                    query.ReaderFunc(reader);
            }
        }

        public IEnumerable<T> Read<T>(SqlDataReader reader)
        {
            var type = typeof(T);
            try
            {
                if (SqlUtil.MethodInfos.ContainsKey(type))
                {
                    if (reader.FieldCount != 1) throw new ApplicationException();
                    Check(type, reader, 0);
                }
                else
                {
                    if (!type.IsPublic) throw new ApplicationException();
                    var propertyInfos = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                    if (reader.FieldCount != propertyInfos.Length) throw new ApplicationException();
                    foreach (var propertyInfo in propertyInfos)
                    {
                        var ordinal = reader.GetOrdinal(propertyInfo.Name);
                        Check(propertyInfo.PropertyType, reader, ordinal);
                    }
                }
                reader.GetMaterializer<T>();
            }
            catch (Exception e)
            {
                WriteSourceCode(reader, type);
                throw new ApplicationException(e.Message, e);
            }
            return Enumerable.Empty<T>();
        }

        private static void Check(Type type, SqlDataReader reader, int ordinal)
        {
            if (AllowDBNull(reader, ordinal))
            {
                if (!(type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Option<>) &&
                      TypesAreCompatible(reader.GetFieldType(ordinal), type.GetGenericArguments().Single())))
                    throw new ApplicationException();
            }
            else
            {
                if (!TypesAreCompatible(reader.GetFieldType(ordinal), type)) throw new ApplicationException();
            }
        }

        private static bool TypesAreCompatible(Type dbType, Type type)
        {
            if (dbType == typeof (int) && type == typeof (MyEnum)) return true;
            return type == dbType;
        }

        private static void WriteSourceCode(SqlDataReader reader, Type type)
        {
            if (!SqlUtil.MethodInfos.ContainsKey(type))
            {
                var properties = string.Join(Environment.NewLine,
                    Enumerable.Range(0, reader.FieldCount).Select(i => {
                        var typeName = (AllowDBNull(reader, i) ? typeof(Option<>).MakeGenericType(reader.GetFieldType(i)) : reader.GetFieldType(i)).GetCSharpName();
                        return $"        public {typeName} {reader.GetName(i)} {{ get; set; }}";
                    }));
                Console.WriteLine($@"    public class {type.GetCSharpName()}
    {{
{properties}
    }}
");
            }
        }

        private static bool AllowDBNull(SqlDataReader reader, int ordinal)
        {
            return (bool)reader.GetSchemaTable().Rows[ordinal]["AllowDBNull"];
        }

        public void NonQuery(NonQuery query)
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