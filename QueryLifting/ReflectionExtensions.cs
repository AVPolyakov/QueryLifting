using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace QueryLifting
{
    public static class ReflectionExtensions
    {
        public static Option<Func<object[], object>> GetStaticInvocation(this MethodBase methodBase)
        {
            Func<object[], object> result;
            if (methodBase.IsStatic)
            {
                result = parameters => methodBase.Invoke(null, parameters);
                return result;
            }
            var compilerGeneratedAttribute = methodBase.DeclaringType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
            if (compilerGeneratedAttribute.Length < 1) return new Option<Func<object[], object>>();
            var infos = methodBase.DeclaringType.GetFields(BindingFlags.Static | BindingFlags.Public).Where(_ => _.FieldType == methodBase.DeclaringType);
            if (infos.Count() != 1) return new Option<Func<object[], object>>();
            result = parameters => methodBase.Invoke(infos.Single().GetValue(null), parameters);
            return result;
        }

        /// <summary>
        /// https://stackoverflow.com/a/33529925
        /// </summary>
        public static string GetCSharpName(this Type type)
        {
            if (_typeToFriendlyName.TryGetValue(type, out var friendlyName))
            {
                return friendlyName;
            }

            friendlyName = type.Name;
            if (type.IsGenericType)
            {
                int backtick = friendlyName.IndexOf('`');
                if (backtick > 0)
                {
                    friendlyName = friendlyName.Remove(backtick);
                }
                friendlyName += "<";
                Type[] typeParameters = type.GetGenericArguments();
                for (int i = 0; i < typeParameters.Length; i++)
                {
                    string typeParamName = typeParameters[i].GetCSharpName();
                    friendlyName += (i == 0 ? typeParamName : ", " + typeParamName);
                }
                friendlyName += ">";
            }

            if (type.IsArray)
            {
                return type.GetElementType().GetCSharpName() + "[]";
            }

            return friendlyName;
        }

        private static readonly Dictionary<Type, string> _typeToFriendlyName = new Dictionary<Type, string> {
            {typeof(string), "string"},
            {typeof(object), "object"},
            {typeof(bool), "bool"},
            {typeof(byte), "byte"},
            {typeof(char), "char"},
            {typeof(decimal), "decimal"},
            {typeof(double), "double"},
            {typeof(short), "short"},
            {typeof(int), "int"},
            {typeof(long), "long"},
            {typeof(sbyte), "sbyte"},
            {typeof(float), "float"},
            {typeof(ushort), "ushort"},
            {typeof(uint), "uint"},
            {typeof(ulong), "ulong"},
            {typeof(void), "void"}
        };
    }
}