using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace SimpleDataAccess
{
    public static partial class QueryExtensions
    {
        public static Query AddParams<T>(this Query query, T param)
        {
            query.DbCommandActions.Add(command => AddParamsCache<T>.Action(command, param));
            return query;
        }

        private static class AddParamsCache<T>
        {
            public static readonly Action<SqlCommand, T> Action;

            static AddParamsCache()
            {
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), null,
                    new[] {typeof(SqlCommand), typeof(T)}, true);

                var ilGenerator = dynamicMethod.GetILGenerator();

                foreach (var info in typeof(T).GetProperties())
                {
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ldstr, "@" + info.Name);
                    ilGenerator.Emit(OpCodes.Ldarg_1);
                    ilGenerator.EmitCall(OpCodes.Callvirt, info.GetGetMethod()!, null);
                    ilGenerator.EmitCall(OpCodes.Call, GetAddParamMethod(info.PropertyType), null);
                    ilGenerator.Emit(OpCodes.Pop);
                }
                ilGenerator.Emit(OpCodes.Ret);

                Action = (Action<SqlCommand, T>)
                    dynamicMethod.CreateDelegate(typeof(Action<SqlCommand, T>));
            }
        }

        private static MethodInfo GetAddParamMethod(Type type)
        {
            if (AddParamsMethods.TryGetValue(type, out var methodInfo))
                return methodInfo;
            if (type.IsEnum)
            {
                //TODO: add code generation for byte, short
                var underlyingType = Enum.GetUnderlyingType(type);
                if (underlyingType == typeof(int))
                    return _intEnumParam.MakeGenericMethod(type);
                if (underlyingType == typeof(long))
                    return _longEnumParam.MakeGenericMethod(type);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var argType = type.GetGenericArguments().Single();
                if (argType.IsEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType(argType);
                    if (underlyingType == typeof(int))
                        return _nullableIntEnumParam.MakeGenericMethod(argType);
                    if (underlyingType == typeof(long))
                        return _nullableLongEnumParam.MakeGenericMethod(argType);
                }
            }
            throw new Exception($"Method of parameter adding not found for type '{type.FullName}'");
        }

        public static readonly Dictionary<Type, MethodInfo> AddParamsMethods = new[]
            {
                GetMethodInfo<Func<SqlCommand, string, int, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, int?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, long, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, long?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, decimal, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, decimal?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, Guid, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, Guid?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, DateTime, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, DateTime?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
                GetMethodInfo<Func<SqlCommand, string, string?, SqlParameter>>((command, name, value) => command.AddParam(name, value)),
            }
            .ToDictionary(methodInfo => methodInfo.GetParameters()[2].ParameterType);

        public static MethodInfo GetMethodInfo<T>(Expression<T> expression)
            => ((MethodCallExpression) expression.Body).Method;

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, int? value)
            => value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.Int) {Value = DBNull.Value});

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, int value)
            => command.Parameters.AddWithValue(parameterName, value);

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, long? value)
            => value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.BigInt) {Value = DBNull.Value});

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, long value)
            => command.Parameters.AddWithValue(parameterName, value);

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, decimal? value)
            => value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.Decimal) {Value = DBNull.Value});

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, decimal value)
        {
            var parameter = command.Parameters.AddWithValue(parameterName, value);
            const byte defaultPrecision = 38;
            if (parameter.Precision < defaultPrecision) parameter.Precision = defaultPrecision;
            const byte defaultScale = 8;
            if (parameter.Scale < defaultScale) parameter.Scale = 8;
            return parameter;
        }

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, Guid value)
            => command.Parameters.AddWithValue(parameterName, value);

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, Guid? value)
            => value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.UniqueIdentifier) {Value = DBNull.Value});

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, DateTime? value)
            => value.HasValue
                ? command.AddParam(parameterName, value.Value)
                : command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.DateTime) {Value = DBNull.Value});

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, DateTime value)
            => command.Parameters.AddWithValue(parameterName, value);

        public static SqlParameter AddParam(this SqlCommand command, string parameterName, string? value)
        {
            var parameter = value == null
                ? command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.NVarChar) {Value = DBNull.Value})
                : command.Parameters.AddWithValue(parameterName, value);
            if (parameter.Size < DefaultLength && parameter.Size >= 0) parameter.Size = DefaultLength;
            return parameter;
        }

        public const int DefaultLength = 4000;

        private static readonly MethodInfo _intEnumParam = GetMethodInfo<Func<SqlCommand, string, BindingFlags, SqlParameter>>(
            (command, name, value) => command.AddIntEnumParam(name, value)).GetGenericMethodDefinition();

        private static readonly MethodInfo _longEnumParam = GetMethodInfo<Func<SqlCommand, string, BindingFlags, SqlParameter>>(
            (command, name, value) => command.AddLongEnumParam(name, value)).GetGenericMethodDefinition();

        private static readonly MethodInfo _nullableIntEnumParam = GetMethodInfo<Func<SqlCommand, string, BindingFlags?, SqlParameter>>(
            (command, name, value) => command.AddIntEnumParam(name, value)).GetGenericMethodDefinition();
        
        private static readonly MethodInfo _nullableLongEnumParam = GetMethodInfo<Func<SqlCommand, string, BindingFlags?, SqlParameter>>(
            (command, name, value) => command.AddLongEnumParam(name, value)).GetGenericMethodDefinition();
        
        public static SqlParameter AddIntEnumParam<T>(this SqlCommand command, string parameterName, T value)
            where T : Enum 
            => command.AddParam(parameterName, ToIntCache<T>.Func(value));

        public static SqlParameter AddLongEnumParam<T>(this SqlCommand command, string parameterName, T value)
            where T : Enum
            => command.AddParam(parameterName, ToLongCache<T>.Func(value));

        public static SqlParameter AddIntEnumParam<T>(this SqlCommand command, string parameterName, T? value)
            where T : struct, Enum
        {
            int? intValue;
            if (value.HasValue)
                intValue = ToIntCache<T>.Func(value.Value);
            else
                intValue = null;
            return command.AddParam(parameterName, intValue);
        }
        
        public static SqlParameter AddLongEnumParam<T>(this SqlCommand command, string parameterName, T? value)
            where T : struct, Enum
        {
            long? intValue;
            if (value.HasValue)
                intValue = ToLongCache<T>.Func(value.Value);
            else
                intValue = null;
            return command.AddParam(parameterName, intValue);
        }
        
        private static class ToIntCache<T>
        {
            public static readonly Func<T, int> Func;

            static ToIntCache()
            {
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof(int),
                    new[] {typeof(T)}, true);
                var ilGenerator = dynamicMethod.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ret);
                Func = (Func<T, int>) dynamicMethod.CreateDelegate(typeof(Func<T, int>));
            }
        }

        private static class ToLongCache<T>
        {
            public static readonly Func<T, long> Func;

            static ToLongCache()
            {
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof(long),
                    new[] {typeof(T)}, true);
                var ilGenerator = dynamicMethod.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ret);
                Func = (Func<T, long>) dynamicMethod.CreateDelegate(typeof(Func<T, long>));
            }
        }
    }
}