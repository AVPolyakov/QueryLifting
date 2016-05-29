using System;
using System.Collections.Generic;
using System.Linq;

namespace QueryLifting
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<TResult>> GetAllCombinations<T, TResult>(this IEnumerable<T> items, Func<T, IEnumerable<TResult>> choiceFunc)
        {
            return items.Aggregate(Enumerable.Repeat(Enumerable.Empty<TResult>(), 1),
                (seed, item) => choiceFunc(item).SelectMany(choiceItem => seed.Select(resultItem => resultItem.Concat(new[] {choiceItem}))));
        }
    }
}