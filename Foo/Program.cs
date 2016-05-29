using System;
using QueryLifting;

namespace Foo
{
    public class Program
    {
        static void Main()
        {
            SqlUtil.ConnectionStringFunc = () => ConnectionString;

            new Foo().M1(new DateTime(2015, 1, 1));
        }

        //Scripts for database are located in folder DatabaseScripts in project root.
        public static string ConnectionString => @"Data Source=(local)\SQL2014;Initial Catalog=QueryLifting;Integrated Security=True";
    }
}
