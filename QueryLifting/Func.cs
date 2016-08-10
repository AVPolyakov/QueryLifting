using System;

namespace QueryLifting
{
    public static class Func
    {
        public static TResult Apply<T, TResult>(this T p, Func<T, TResult> func)
        {
            return func(p);
        }

        public static Func<T> New<T>(Func<T> func)
        {
            return func;
        }
    }
}