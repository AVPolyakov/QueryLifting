using System;
using System.CodeDom;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CSharp;

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

        public static string GetCSharpName(this Type type)
        {
            using (var provider = new CSharpCodeProvider())
            {
                var output = provider.GetTypeOutput(new CodeTypeReference(type));
                var ns = $"{type.Namespace}.";
                return output.StartsWith(ns) ? output.Substring(ns.Length) : output;
            }
        }
    }
}