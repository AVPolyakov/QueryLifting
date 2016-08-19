using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace QueryLifting
{
    public static class SqlUtil
    {
        public static Query<T> Query<T>(this SqlCommand command, Func<SqlDataReader, T> readerFunc, Option<string> connectionString = new Option<string>())
        {
            return new Query<T>(command, readerFunc, connectionString);
        }

        public static NonQuery NonQuery(this SqlCommand command, Option<string> connectionString = new Option<string>())
        {
            return new NonQuery(command, connectionString);
        }

        public static IEnumerable<T> Read<T>(this SqlCommand command, Func<SqlDataReader, T> materializer,
            Option<string> connectionString = new Option<string>())
        {
            return command.Query(reader => reader.Read(() => materializer(reader)), connectionString).Read();
        }

        public static IEnumerable<T> Read<T>(this SqlCommand command, Option<string> connectionString = new Option<string>())
        {
            return command.Query<T>(connectionString).Read();
        }

        public static Query<IEnumerable<T>> Query<T>(this SqlCommand command, Option<string> connectionString = new Option<string>())
        {
            return command.Query(Read<T>, connectionString);
        }

        public static IEnumerable<T> Read<T>(this SqlDataReader reader)
        {
            return Read(reader, reader.GetMaterializer<T>());
        }

        public static IEnumerable<T> Read<T>(this SqlDataReader reader, Func<T> materializer)
        {
            return QueryChecker != null ? QueryChecker.Read(reader, materializer) : GetEnumerable(reader, materializer);
        }

        private static IEnumerable<T> GetEnumerable<T>(SqlDataReader reader, Func<T> materializer)
        {
            while (reader.Read()) yield return materializer();
        }

        public static Func<string> ConnectionStringFunc = () => { throw new ApplicationException("Set the connection string func at application start."); };

        public static IEnumerable<T> Read<T>(this Query<IEnumerable<T>> query)
        {
            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, ConnectionStringFunc)))
            {
                query.Command.Connection = connection;
                connection.Open();
                using (var reader = query.Command.ExecuteReader())
                    foreach (var item in query.ReaderFunc(reader)) yield return item;
            }
        }

        public static T Read<T>(this Query<T> query)
        {
            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, ConnectionStringFunc)))
            {
                query.Command.Connection = connection;
                connection.Open();
                using (var reader = query.Command.ExecuteReader())
                    return query.ReaderFunc(reader);
            }
        }

        public static int Execute(this NonQuery query)
        {
            using (var connection = new SqlConnection(query.ConnectionString.Match(_ => _, ConnectionStringFunc)))
            {
                query.Command.Connection = connection;
                connection.Open();
                return query.Command.ExecuteNonQuery();
            }
        }

        public static int Int32(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null ? QueryChecker.Check<int>(reader, ordinal) : reader.GetInt32(ordinal);
        }

        public static Option<int> OptionInt32(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null 
                ? QueryChecker.Check<Option<int>>(reader, ordinal) 
                : (reader.IsDBNull(ordinal) ? new Option<int>() : reader.GetInt32(ordinal));
        }

        public static decimal Decimal(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null ? QueryChecker.Check<decimal>(reader, ordinal) : reader.GetDecimal(ordinal);
        }

        public static Option<decimal> OptionDecimal(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null 
                ? QueryChecker.Check<Option<decimal>>(reader, ordinal) 
                : (reader.IsDBNull(ordinal) ? new Option<decimal>() : reader.GetDecimal(ordinal));
        }

        public static Guid Guid(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null ? QueryChecker.Check<Guid>(reader, ordinal) : reader.GetGuid(ordinal);
        }

        public static Option<Guid> OptionGuid(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null 
                ? QueryChecker.Check<Option<Guid>>(reader, ordinal) 
                : (reader.IsDBNull(ordinal) ? new Option<Guid>() : reader.GetGuid(ordinal));
        }

        public static DateTime DateTime(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null ? QueryChecker.Check<DateTime>(reader, ordinal) : reader.GetDateTime(ordinal);
        }

        public static Option<DateTime> OptionDateTime(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null 
                ? QueryChecker.Check<Option<DateTime>>(reader, ordinal) 
                : (reader.IsDBNull(ordinal) ? new Option<DateTime>() : reader.GetDateTime(ordinal));
        }

        public static string String(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null ? QueryChecker.Check<string>(reader, ordinal) : reader.GetString(ordinal);
        }

        public static Option<string> OptionString(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null 
                ? QueryChecker.Check<Option<string>>(reader, ordinal) 
                : (reader.IsDBNull(ordinal) ? new Option<string>() : reader.GetString(ordinal));
        }

        public static bool Boolean(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null ? QueryChecker.Check<bool>(reader, ordinal) : reader.GetBoolean(ordinal);
        }

        public static Option<bool> OptionBoolean(this SqlDataReader reader, int ordinal)
        {
            return QueryChecker != null 
                ? QueryChecker.Check<Option<bool>>(reader, ordinal) 
                : (reader.IsDBNull(ordinal) ? new Option<bool>() : reader.GetBoolean(ordinal));
        }

        /// <summary>
        /// Generates in runtime the code to retrieve the data from DataReader for all properties of type T.
        /// Returns the function that creates the instance of type T and populates the instance properties 
        /// from DataReader.
        /// </summary>
        public static Func<T> GetMaterializer<T>(this SqlDataReader reader)
        {
            return Cache<T>.Func(reader);
        }

        public static readonly Dictionary<Type, MethodInfo> MethodInfos = new[] {
            GetMethodInfo<Func<SqlDataReader, int, int>>((reader, i) => reader.Int32(i)),
            GetMethodInfo<Func<SqlDataReader, int, Option<int>>>((reader, i) => reader.OptionInt32(i)),
            GetMethodInfo<Func<SqlDataReader, int, decimal>>((reader, i) => reader.Decimal(i)),
            GetMethodInfo<Func<SqlDataReader, int, Option<decimal>>>((reader, i) => reader.OptionDecimal(i)),
            GetMethodInfo<Func<SqlDataReader, int, Guid>>((reader, i) => reader.Guid(i)),
            GetMethodInfo<Func<SqlDataReader, int, Option<Guid>>>((reader, i) => reader.OptionGuid(i)),
            GetMethodInfo<Func<SqlDataReader, int, DateTime>>((reader, i) => reader.DateTime(i)),
            GetMethodInfo<Func<SqlDataReader, int, Option<DateTime>>>((reader, i) => reader.OptionDateTime(i)),
            GetMethodInfo<Func<SqlDataReader, int, string>>((reader, i) => reader.String(i)),
            GetMethodInfo<Func<SqlDataReader, int, Option<string>>>((reader, i) => reader.OptionString(i)),
            GetMethodInfo<Func<SqlDataReader, int, bool>>(((reader, i) => reader.Boolean(i))),
            GetMethodInfo<Func<SqlDataReader, int, Option<bool>>>((reader, i) => reader.OptionBoolean(i))
        }.ToDictionary(_ => _.ReturnType);

        private static class Cache<T>
        {
            public static readonly Func<SqlDataReader, Func<T>> Func;

            static Cache()
            {
                Func<SqlDataReader, Func<T>> func;
                MethodInfo methodInfo;
                if (MethodInfos.TryGetValue(typeof (T), out methodInfo))
                {
                    var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof (T),
                        new[] {typeof (SqlDataReader)}, true);
                    var ilGenerator = dynamicMethod.GetILGenerator();
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ldc_I4_0);
                    ilGenerator.EmitCall(methodInfo.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, methodInfo, null);
                    ilGenerator.Emit(OpCodes.Ret);
                    dynamicMethod.DefineParameter(1, ParameterAttributes.In, "arg1");
                    var @delegate = (Func<SqlDataReader, T>) dynamicMethod.CreateDelegate(typeof (Func<SqlDataReader, T>));
                    func = reader => () => @delegate(reader);
                }
                else
                {
                    var typeBuilder = moduleBuilder.DefineType("T" + System.Guid.NewGuid().ToString("N"), TypeAttributes.NotPublic,
                        null, new[] {typeof (IMaterializer<T>)});
                    var list = typeof (T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Select(property => Tuple.Create(property, typeBuilder.DefineField(property.Name, typeof (int), FieldAttributes.Public))).ToList();
                    var methodBuilder = typeBuilder.DefineMethod(nameof(IMaterializer<object>.Materialize),
                        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final |
                            MethodAttributes.NewSlot, typeof (T), new[] {typeof (SqlDataReader)});
                    var generator = methodBuilder.GetILGenerator();
                    generator.DeclareLocal(typeof (T));
                    generator.Emit(OpCodes.Newobj, typeof (T).GetConstructor(new Type[] {}));
                    generator.Emit(OpCodes.Stloc_0);
                    foreach (var item in list)
                    {
                        generator.Emit(OpCodes.Ldloc_0);
                        generator.Emit(OpCodes.Ldarg_1);
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldfld, item.Item2);
                        var info = MethodInfos[item.Item1.PropertyType];
                        generator.EmitCall(info.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, info, null);
                        generator.EmitCall(OpCodes.Callvirt, item.Item1.GetSetMethod(), null);
                    }
                    generator.Emit(OpCodes.Ldloc_0);
                    generator.Emit(OpCodes.Ret);
                    var type = typeBuilder.CreateType();
                    var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof (IMaterializer<T>),
                        new[] {typeof (SqlDataReader)}, true);
                    var ilGenerator = dynamicMethod.GetILGenerator();
                    ilGenerator.DeclareLocal(type);
                    ilGenerator.Emit(OpCodes.Newobj, type.GetConstructor(new Type[] {}));
                    ilGenerator.Emit(OpCodes.Stloc_0);
                    foreach (var fieldInfo in type.GetFields())
                    {
                        ilGenerator.Emit(OpCodes.Ldloc_0);
                        ilGenerator.Emit(OpCodes.Ldarg_0);
                        ilGenerator.Emit(OpCodes.Ldstr, fieldInfo.Name);
                        ilGenerator.EmitCall(OpCodes.Call, GetMethodInfo<Func<SqlDataReader, string, int>>((reader, name) => reader.Ordinal(name)), null);
                        ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
                    }
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                    ilGenerator.Emit(OpCodes.Ret);
                    dynamicMethod.DefineParameter(1, ParameterAttributes.In, "arg1");
                    var @delegate = (Func<SqlDataReader, IMaterializer<T>>) dynamicMethod.CreateDelegate(typeof (Func<SqlDataReader, IMaterializer<T>>));
                    func = reader => {
                        var materializer = @delegate(reader);
                        return () => materializer.Materialize(reader);
                    };
                }
                Func = func;
            }
        }

        public static int Ordinal(this SqlDataReader reader, string name)
        {
            return QueryChecker != null ? QueryChecker.GetOrdinal(reader, name) : reader.GetOrdinal(name);
        }

        private static readonly ModuleBuilder moduleBuilder;

        static SqlUtil()
        {
            var assemblyName = new AssemblyName {Name = System.Guid.NewGuid().ToString("N")};
            moduleBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,
                AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);
        }

        public interface IMaterializer<out T>
        {
            T Materialize(SqlDataReader reader);
        }

        public static IQueryChecker QueryChecker { get; set; }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, int? value)
        {
            return value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.Int) {Value = DBNull.Value});
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, int value)
        {
            return command.Parameters.AddWithValue(parameterName, value);
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, decimal? value)
        {
            return value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.Decimal) {Value = DBNull.Value});
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, Guid value)
        {
            return command.Parameters.AddWithValue(parameterName, value);
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, Guid? value)
        {
            return value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.UniqueIdentifier) {Value = DBNull.Value});
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, decimal value)
        {
            return command.Parameters.AddWithValue(parameterName, value);
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, DateTime? value)
        {
            return value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.DateTime) {Value = DBNull.Value});
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, DateTime value)
        {
            return command.Parameters.AddWithValue(parameterName, value);
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, string value)
        {
            var parameter = value == null
                ? command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.NVarChar) {Value = DBNull.Value})
                : command.Parameters.AddWithValue(parameterName, value);
            parameter.Size = -1;
            return parameter;
        }

        public static SqlParameter AddParam<T>(this SqlCommand command, string parameterName, Param<T> param)
        {
            return ParamCache<T>.Func(command, parameterName, param.Value);
        }

        private static class ParamCache<T>
        {
            public static readonly Func<SqlCommand, string, T, SqlParameter> Func;

            static ParamCache()
            {
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof (SqlParameter),
                    new[] {typeof (SqlCommand), typeof (string), typeof (T)}, true);
                var ilGenerator = dynamicMethod.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Ldarg_2);
                ilGenerator.EmitCall(OpCodes.Call, GetAddParamMethod(typeof (T)), null);
                ilGenerator.Emit(OpCodes.Ret);
                dynamicMethod.DefineParameter(1, ParameterAttributes.In, "command");
                dynamicMethod.DefineParameter(2, ParameterAttributes.In, "parameterName");
                dynamicMethod.DefineParameter(3, ParameterAttributes.In, "value");
                Func = (Func<SqlCommand, string, T, SqlParameter>)
                    dynamicMethod.CreateDelegate(typeof (Func<SqlCommand, string, T, SqlParameter>));
            }
        }

        public static SqlCommand AddParams<T>(this SqlCommand command, T param)
        {
            AddParamsCache<T>.Action(command, param);
            return command;
        }

        public static readonly Dictionary<Type, MethodInfo> AddParamsMethods = new[] {
            GetMethodInfo<Func<SqlCommand, string, int, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, int?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, decimal, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, decimal?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, Guid, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, Guid?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, DateTime, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, DateTime?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            GetMethodInfo<Func<SqlCommand, string, string, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
        }.ToDictionary(_ => _.GetParameters()[2].ParameterType);

        private static class AddParamsCache<T>
        {
            public static readonly Action<SqlCommand, T> Action;

            static AddParamsCache()
            {
                if (!typeof (T).IsAnonymousType()) throw new InvalidOperationException();
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), null,
                    new[] {typeof (SqlCommand), typeof(T)}, true);
                var ilGenerator = dynamicMethod.GetILGenerator();
                foreach (var info in typeof (T).GetProperties())
                {
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ldstr, "@" + info.Name);
                    ilGenerator.Emit(OpCodes.Ldarg_1);
                    ilGenerator.EmitCall(OpCodes.Callvirt, info.GetGetMethod(), null);
                    ilGenerator.EmitCall(OpCodes.Call, GetAddParamMethod(info.PropertyType), null);
                    ilGenerator.Emit(OpCodes.Pop);
                }
                ilGenerator.Emit(OpCodes.Ret);
                dynamicMethod.DefineParameter(1, ParameterAttributes.In, "command");
                dynamicMethod.DefineParameter(2, ParameterAttributes.In, "p");
                Action = (Action<SqlCommand, T>)
                    dynamicMethod.CreateDelegate(typeof (Action<SqlCommand, T>));
            }
        }

        private static MethodInfo GetAddParamMethod(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Param<>))
                return paramMethod.MakeGenericMethod(type.GetGenericArguments());
            else
                return AddParamsMethods[type];
        }

        private static readonly MethodInfo paramMethod = GetMethodInfo<Func<SqlCommand, string, Param<object>, SqlParameter>>(
                (command, name, value) => command.AddParam(name, value)).GetGenericMethodDefinition();

        public static StringBuilder Append<T>(this StringBuilder builder, SqlCommand command, string text, T param)
        {
            builder.Append(text);
            command.AddParams(param);
            return builder;
        }

        public static MethodInfo GetMethodInfo<T>(Expression<T> expression)
        {
            return ((MethodCallExpression)expression.Body).Method;
        }

        public static bool IsAnonymousType(this Type type)
        {
            var customAttributes = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
            switch (customAttributes.Length)
            {
                case 0:
                    return false;
                case 1:
                    return type.Name.Contains("AnonymousType");
                default:
                    throw new ApplicationException();
            }
        }

        public static Query<IEnumerable<TKey>> InsertQuery<T, TKey>(TKey prototype, string table, T p, Option<string> connectionString = new Option<string>())
        {
            var command = new SqlCommand();
            var columns = GetColumns(table, connectionString);
            var columnsClause = string.Join(",", from _ in columns where !_.IsAutoIncrement select _.ColumnName);
            var outClause = columns.Single(_ => _.IsKey).ColumnName;
            var valuesClause = string.Join(",", from _ in columns where !_.IsAutoIncrement select $"@{_.ColumnName}");
            command.CommandText = new StringBuilder().Append(command, $@"
INSERT INTO {table} ({columnsClause}) 
OUTPUT inserted.{outClause}
VALUES ({valuesClause})", p).ToString();
            return command.Query<TKey>();
        }

        public static NonQuery UpdateQuery<T>(string table, T p, Option<string> connectionString = new Option<string>())
        {
            var command = new SqlCommand();
            var columns = GetColumns(table, connectionString);
            var setClause = string.Join(",", from _ in columns where !_.IsKey select $"{_.ColumnName}=@{_.ColumnName}");
            var whereClause = string.Join(" AND ", from _ in columns where _.IsKey select $"{_.ColumnName}=@{_.ColumnName}");
            command.CommandText = new StringBuilder().Append(command, $@"
UPDATE {table}
SET {setClause}
WHERE {whereClause}", p).ToString();
            return command.NonQuery();
        }

        public static NonQuery DeleteQuery<T>(string table, T p, Option<string> connectionString = new Option<string>())
        {
            var command = new SqlCommand();
            var columns = GetColumns(table, connectionString);
            var whereClause = string.Join(" AND ", from _ in columns where _.IsKey select $"{_.ColumnName}=@{_.ColumnName}");
            command.CommandText = new StringBuilder().Append(command, $@"
DELETE FROM {table}
WHERE {whereClause}", p).ToString();
            return command.NonQuery();
        }

        private static List<ColumnInfo> GetColumns(string table, Option<string> connectionString)
        {
            List<ColumnInfo> value;
            //TODO: add connection string to key
            if (!columnDictionary.TryGetValue(table, out value))
            {
                value = GetColumnEnumerable(table, connectionString).ToList();
                columnDictionary[table] = value;
            }
            return value;
        }

        private static readonly ConcurrentDictionary<string, List<ColumnInfo>> columnDictionary =
            new ConcurrentDictionary<string, List<ColumnInfo>>();

        private static IEnumerable<ColumnInfo> GetColumnEnumerable(string table, Option<string> connectionString)
        {
            using (var connection = new SqlConnection(connectionString.Match(_ => _, ConnectionStringFunc)))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM {table}";
                    using (var reader = command.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
                    {
                        var schemaTable = reader.GetSchemaTable();
                        foreach (DataRow dataRow in schemaTable.Rows)
                            yield return new ColumnInfo(
                                (string)dataRow["ColumnName"],
                                true.Equals(dataRow["IsKey"]),
                                true.Equals(dataRow["IsAutoIncrement"]));
                    }
                }
            }
        }
    }

    internal class ColumnInfo
    {
        public string ColumnName { get; }
        public bool IsKey { get; }
        public bool IsAutoIncrement { get; }

        public ColumnInfo(string columnName, bool isKey, bool isAutoIncrement)
        {
            ColumnName = columnName;
            IsKey = isKey;
            IsAutoIncrement = isAutoIncrement;
        }
    }
}