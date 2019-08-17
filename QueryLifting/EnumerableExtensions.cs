using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace QueryLifting
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<TResult>> GetAllCombinations<TResult>(this IEnumerable<ParameterInfo> items, Func<ParameterInfo, IEnumerable<TResult>> choiceFunc)
        {
            var clusterParameters = items.Where(IsCluster);
            if (clusterParameters.Any())
            {
                return clusterParameters.SelectMany(
                    clusterParameter => GetAllCombinationsOfItems(
                        items,
                        parameterInfo =>
                        {
                            var enumerable = choiceFunc(parameterInfo);
                            return parameterInfo.Equals(clusterParameter) || !IsCluster(parameterInfo)
                                ? enumerable
                                : new[] {enumerable.First()};
                        })
                );
            }
            else
                return items.GetAllCombinationsOfItems(choiceFunc);
        }

        public static IEnumerable<IEnumerable<TResult>> GetAllCombinationsOfItems<T, TResult>(this IEnumerable<T> items, Func<T, IEnumerable<TResult>> choiceFunc)
        {
            if (FirstOnly) return new[] {items.Select(_ => choiceFunc(_).First())};
            return items.Aggregate(Enumerable.Repeat(Enumerable.Empty<TResult>(), 1),
                (seed, item) => choiceFunc(item).SelectMany(choiceItem => seed.Select(resultItem => resultItem.Concat(new[] {choiceItem}))));
        }

        private static bool IsCluster(ParameterInfo parameterInfo)
        {
            return parameterInfo.GetCustomAttributes(typeof(ClusterAttribute), true).Length > 0 ||
                parameterInfo.ParameterType.GetCustomAttributes(typeof(ClusterAttribute), true).Length > 0;
        }

        private static readonly AsyncLocal<bool> firstOnly = new AsyncLocal<bool>();

        public static bool FirstOnly
        {
            get => firstOnly.Value;
            set => firstOnly.Value = value;
        }
    }
}