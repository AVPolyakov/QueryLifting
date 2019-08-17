using System;
using System.Linq;
using QueryLifting;
using Xunit;

namespace Foo.Tests
{
    public class EnumerableExtensionsTests
    {
        [Fact]
        public void GetAllCombinations()
        {
            {
                var parameterInfos = new
                {
                    A1 = new DateTime?(),
                    A2 = new DateTime?(),
                    A3 = new DateTime?(),
                    A4 = new DateTime?(),
                    A5 = new DateTime?(),
                }.GetType().GetConstructors().Single().GetParameters();
                Assert.Equal(32, parameterInfos.GetAllCombinations(QueryTests.TestValues).Count());
            }
            {
                var parameterInfos = new
                {
                    A1 = new DateTime?().Cluster(),
                    A2 = new DateTime?().Cluster(),
                    A3 = new DateTime?().Cluster(),
                    A4 = new DateTime?().Cluster(),
                    A5 = new DateTime?().Cluster(),
                }.GetType().GetConstructors().Single().GetParameters();
                Assert.Equal(10, parameterInfos.GetAllCombinations(QueryTests.TestValues).Count());
            }
        }
    }
}