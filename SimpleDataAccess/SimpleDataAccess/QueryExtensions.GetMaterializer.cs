using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SimpleDataAccess
{
    public static partial class QueryExtensions
    {
        /// <summary>
        /// Generates in runtime the code to retrieve the data from DataReader for all properties of type T.
        /// Returns the function that creates the instance of type T and populates the instance properties 
        /// from DataReader.
        /// </summary>
        public static Func<T> GetMaterializer<T>(this SqlDataReader reader) => Cache<T>.Func(reader);
        
        private static class Cache<T>
        {
            public static readonly Func<SqlDataReader, Func<T>> Func;

            static Cache()
            {
                Func<SqlDataReader, Func<T>> func;
                
                var readMethod = GetReadMethod(typeof(T));
                if (readMethod != null)
                {
                    var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof(T),
                        new[] {typeof(SqlDataReader)}, true);
                    
                    var ilGenerator = dynamicMethod.GetILGenerator();
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ldc_I4_0);
                    ilGenerator.EmitCall(readMethod.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, readMethod, null);
                    ilGenerator.Emit(OpCodes.Ret);
                    
                    var @delegate = (Func<SqlDataReader, T>) dynamicMethod.CreateDelegate(typeof(Func<SqlDataReader, T>));
                    func = reader => () => @delegate(reader);
                }
                else
                {
                    var typeBuilder = _moduleBuilder.DefineType("T" + System.Guid.NewGuid().ToString("N"), TypeAttributes.NotPublic,
                        null, new[] {typeof(IMaterializer<T>)});
                    
                    var list = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Select(property => Tuple.Create(property, typeBuilder.DefineField(property.Name, typeof(int), FieldAttributes.Public))).ToList();
                    
                    var methodBuilder = typeBuilder.DefineMethod(nameof(IMaterializer<object>.Materialize),
                        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final |
                        MethodAttributes.NewSlot, typeof(T), new[] {typeof(SqlDataReader)});
                    
                    var generator = methodBuilder.GetILGenerator();
                    generator.DeclareLocal(typeof(T));
                    generator.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Array.Empty<Type>())!);
                    generator.Emit(OpCodes.Stloc_0);
                    foreach (var item in list)
                    {
                        generator.Emit(OpCodes.Ldloc_0);
                        generator.Emit(OpCodes.Ldarg_1);
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldfld, item.Item2);
                        var method = GetReadMethod(item.Item1.PropertyType);
                        if (method == null)
                            throw new Exception($"Read method not fount for type '{item.Item1.PropertyType.FullName}'");
                        generator.EmitCall(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method, null);
                        generator.EmitCall(OpCodes.Callvirt, item.Item1.GetSetMethod()!, null);
                    }
                    generator.Emit(OpCodes.Ldloc_0);
                    generator.Emit(OpCodes.Ret);
                    
                    var type = typeBuilder.CreateTypeInfo()!;
                    var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof(IMaterializer<T>),
                        new[] {typeof(SqlDataReader)}, true);
                    var ilGenerator = dynamicMethod.GetILGenerator();
                    ilGenerator.DeclareLocal(type);
                    ilGenerator.Emit(OpCodes.Newobj, type.GetConstructor(Array.Empty<Type>())!);
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
                    
                    var @delegate = (Func<SqlDataReader, IMaterializer<T>>) dynamicMethod.CreateDelegate(typeof(Func<SqlDataReader, IMaterializer<T>>));
                    func = reader =>
                    {
                        var materializer = @delegate(reader);
                        return () => materializer.Materialize(reader);
                    };
                }
                
                Func = func;
            }
        }
        
        public static int Ordinal(this SqlDataReader reader, string name) => reader.GetOrdinal(name);
        
        public interface IMaterializer<out T>
        {
            T Materialize(SqlDataReader reader);
        }

        private static readonly ModuleBuilder _moduleBuilder;

        static QueryExtensions()
        {
            var assemblyName = new AssemblyName {Name = System.Guid.NewGuid().ToString("N")};
            _moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName,
                AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);
        }
        
        private static MethodInfo? GetReadMethod(Type type)
        {
            if (ReadMethodInfos.TryGetValue(type, out var methodInfo))
                return methodInfo;
            if (type.IsEnum)
            {
                //TODO: add code generation for byte, short
                var underlyingType = Enum.GetUnderlyingType(type);
                if (underlyingType == typeof(int))
                    return _intEnum.MakeGenericMethod(type);
                if (underlyingType == typeof(long))
                    return _longEnum.MakeGenericMethod(type);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var argType = type.GetGenericArguments().Single();
                if (argType.IsEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType(argType);
                    if (underlyingType == typeof(int))
                        return _nullableIntEnum.MakeGenericMethod(argType);
                    if (underlyingType == typeof(long))
                        return _nullableLongEnum.MakeGenericMethod(argType);
                }
            }
            return null;
        }

        public static readonly Dictionary<Type, MethodInfo> ReadMethodInfos = new[]
            {
                GetMethodInfo<Func<SqlDataReader, int, int>>((reader, i) => reader.Int32(i)),
                GetMethodInfo<Func<SqlDataReader, int, int?>>((reader, i) => reader.NullableInt32(i)),
                GetMethodInfo<Func<SqlDataReader, int, long>>((reader, i) => reader.Int64(i)),
                GetMethodInfo<Func<SqlDataReader, int, long?>>((reader, i) => reader.NullableInt64(i)),
                GetMethodInfo<Func<SqlDataReader, int, decimal>>((reader, i) => reader.Decimal(i)),
                GetMethodInfo<Func<SqlDataReader, int, decimal?>>((reader, i) => reader.NullableDecimal(i)),
                GetMethodInfo<Func<SqlDataReader, int, Guid>>((reader, i) => reader.Guid(i)),
                GetMethodInfo<Func<SqlDataReader, int, Guid?>>((reader, i) => reader.NullableGuid(i)),
                GetMethodInfo<Func<SqlDataReader, int, DateTime>>((reader, i) => reader.DateTime(i)),
                GetMethodInfo<Func<SqlDataReader, int, DateTime?>>((reader, i) => reader.NullableDateTime(i)),
                GetMethodInfo<Func<SqlDataReader, int, string?>>((reader, i) => reader.String(i)),
                GetMethodInfo<Func<SqlDataReader, int, bool>>(((reader, i) => reader.Boolean(i))),
                GetMethodInfo<Func<SqlDataReader, int, bool?>>((reader, i) => reader.NullableBoolean(i))
            }
            .ToDictionary(methodInfo => methodInfo.ReturnType);
        
        public static int Int32(this SqlDataReader reader, int ordinal) 
            => reader.GetInt32(ordinal);

        public static long? NullableInt64(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? new long?() : reader.GetInt64(ordinal);
        
        public static long Int64(this SqlDataReader reader, int ordinal)
            => reader.GetInt64(ordinal);

        public static int? NullableInt32(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? new int?() : reader.GetInt32(ordinal);
        
        public static decimal Decimal(this SqlDataReader reader, int ordinal)
            => reader.GetDecimal(ordinal);

        public static decimal? NullableDecimal(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? new decimal?() : reader.GetDecimal(ordinal);

        public static Guid Guid(this SqlDataReader reader, int ordinal)
            => reader.GetGuid(ordinal);

        public static Guid? NullableGuid(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? new Guid?() : reader.GetGuid(ordinal);
        
        public static DateTime DateTime(this SqlDataReader reader, int ordinal)
            => reader.GetDateTime(ordinal);

        public static DateTime? NullableDateTime(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? new DateTime?() : reader.GetDateTime(ordinal);

        public static string? String(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        
        public static bool Boolean(this SqlDataReader reader, int ordinal)
            => reader.GetBoolean(ordinal);

        public static bool? NullableBoolean(this SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? new bool?() : reader.GetBoolean(ordinal);
        
        private static readonly MethodInfo _intEnum = GetMethodInfo<Func<SqlDataReader, int, BindingFlags>>(
            (reader, ordinal) => reader.IntEnum<BindingFlags>(ordinal)).GetGenericMethodDefinition();
        
        private static readonly MethodInfo _longEnum = GetMethodInfo<Func<SqlDataReader, int, BindingFlags>>(
            (reader, ordinal) => reader.LongEnum<BindingFlags>(ordinal)).GetGenericMethodDefinition();
        
        private static readonly MethodInfo _nullableIntEnum = GetMethodInfo<Func<SqlDataReader, int, BindingFlags?>>(
            (reader, ordinal) => reader.NullableIntEnum<BindingFlags>(ordinal)).GetGenericMethodDefinition();

        private static readonly MethodInfo _nullableLongEnum = GetMethodInfo<Func<SqlDataReader, int, BindingFlags?>>(
            (reader, ordinal) => reader.NullableLongEnum<BindingFlags>(ordinal)).GetGenericMethodDefinition();

        public static T IntEnum<T>(this SqlDataReader reader, int ordinal)
            where T : Enum
            => IntToEnumCache<T>.Func(reader.GetInt32(ordinal));
        
        public static T LongEnum<T>(this SqlDataReader reader, int ordinal)
            where T : Enum
            => LongToEnumCache<T>.Func(reader.GetInt64(ordinal));

        public static T? NullableIntEnum<T>(this SqlDataReader reader, int ordinal)
            where T : struct, Enum
            => reader.IsDBNull(ordinal)
                ? new T?()
                : IntToEnumCache<T>.Func(reader.GetInt32(ordinal));

        public static T? NullableLongEnum<T>(this SqlDataReader reader, int ordinal)
            where T : struct, Enum
            => reader.IsDBNull(ordinal)
                ? new T?()
                : LongToEnumCache<T>.Func(reader.GetInt64(ordinal));

        private static class IntToEnumCache<T>
        {
            public static readonly Func<int, T> Func;

            static IntToEnumCache()
            {
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof(T),
                    new[] {typeof(int)}, true);
                var ilGenerator = dynamicMethod.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ret);
                Func = (Func<int, T>) dynamicMethod.CreateDelegate(typeof(Func<int, T>));
            }
        }
        
        private static class LongToEnumCache<T>
        {
            public static readonly Func<long, T> Func;

            static LongToEnumCache()
            {
                var dynamicMethod = new DynamicMethod(System.Guid.NewGuid().ToString("N"), typeof(T),
                    new[] {typeof(long)}, true);
                var ilGenerator = dynamicMethod.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ret);
                Func = (Func<long, T>) dynamicMethod.CreateDelegate(typeof(Func<long, T>));
            }
        }
    }
}