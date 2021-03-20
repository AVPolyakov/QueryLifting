using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SimpleDataAccess.Tests
{
    public class Tests:  IClassFixture<DatabaseFixture>
    {
        private static ConnectionInfo Db => DatabaseFixture.Db;

        [Fact]
        public async Task Posts_Success()
        {
            var date = new DateTime(2015, 1, 1);
            
            var query = Db.Query(@"
SELECT p.PostId, p.Text, p.CreationDate
FROM Post p
WHERE p.CreationDate >= @date
ORDER BY p.PostId", new {date});

            var postInfos = await query.ToList<PostInfo>();
            
            Assert.Equal(2, postInfos.Count);

            Assert.NotEqual(default, postInfos[0].PostId);
            Assert.NotEqual(default, postInfos[1].PostId);
            
            Assert.Equal("Test1", postInfos[0].Text);
            Assert.Null(postInfos[1].Text);
            
            Assert.Equal(new DateTime(2021, 01, 14), postInfos[0].CreationDate);
            Assert.Equal(new DateTime(2021, 02, 15), postInfos[1].CreationDate);
        }
        
        [Fact]
        public async Task Posts_DynamicSql_Success()
        {
            {
                var postInfos = await GetPosts(new DateTime(2015, 1, 1));
                Assert.Equal(2, postInfos.Count);
            }
            {
                var postInfos = await GetPosts(new DateTime(3015, 1, 1));
                Assert.Empty(postInfos);
            }
        }

        private static Task<List<PostInfo>> GetPosts(DateTime? date)
        {
            var query = Db.Query(@"
SELECT p.PostId, p.Text, p.CreationDate
FROM Post p");
            if (date.HasValue)
                query.AppendLine(@"
WHERE p.CreationDate >= @date", new {date});

            return query.ToList<PostInfo>();
        }

        [Fact]
        public async Task ScalarType_Success()
        {
            var single = await Db.Query("SELECT @A1 AS A1",
                    new
                    {
                        A1 = "Test3"
                    })
                .Single<string>();
            
            Assert.Equal("Test3", single);
        }        
        
        [Fact]
        public async Task Enum_Success()
        {
            A1? a2 = A1.Item2;
            A1? a3 = null;
            A2? a5 = A2.Item2;
            A2? a6 = null;
            
            var record1 = await Db.Query(@"
SELECT 
    @A1 AS A1,
    @A2 AS A2,
    @A3 AS A3,
    @A4 AS A4,
    @A5 AS A5,
    @A6 AS A6
",
                    new
                    {
                        A1 = A1.Item2,
                        A2 = a2,
                        A3 = a3,
                        A4 = A2.Item2,
                        A5 = a5,
                        A6 = a6,
                    })
                .Single<Record1>();
            
            Assert.Equal(A1.Item2, record1.A1);
            Assert.Equal(a2, record1.A2);
            Assert.Equal(a3, record1.A3);
            Assert.Equal(A2.Item2, record1.A4);
            Assert.Equal(a5, record1.A5);
            Assert.Equal(a6, record1.A6);
        }        
    }
}