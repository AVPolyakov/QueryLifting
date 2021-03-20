using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleDataAccess
{
    public static partial class QueryExtensions
    {
        public static Query AppendLine(this Query query, string queryText)
        {
            query.StringBuilder.AppendLine(queryText);
            return query;
        }
        
        public static Query AppendLine<T>(this Query query, string queryText, T param)
        {
            query.AppendLine(queryText).AddParams(param);
            return query;
        }
        
        public static async Task<List<T>> ToList<T>(this Query query)
        {
            await using (var connection = new SqlConnection(query.ConnectionInfo.ConnectionString))
            {
                await connection.OpenAsync();

                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = query.StringBuilder.ToString();

                    foreach (var dbCommandAction in query.DbCommandActions) 
                        dbCommandAction(command);
                    
                    await using (var reader = await command.ExecuteReaderAsync())
                    {
                        var materializer = reader.GetMaterializer<T>();

                        var result = new List<T>();
                        
                        while (await reader.ReadAsync())
                            result.Add(materializer());
                        
                        return result;
                    }
                }
            }
        }

        public static async Task<T> Single<T>(this Query query)
        {
            var list = await query.ToList<T>();
            return list.Single();
        }
    }
}