// using Microsoft.Data.Sqlite;
// using Newtonsoft.Json;
//
// namespace YT_Downloader_DiscordBot;
//
// public class Database
// {
//     public static Database Instance { get; private set; }
//     static Database()
//     {
//         Instance = new Database(Config.Current.DatabasePath);
//     }
//     
//     private readonly SqliteConnection m_connection;
//
//     private Database(string connectionString)
//     {
//         m_connection = new SqliteConnection(connectionString);
//         m_connection.Open();
//     }
//     ~Database()
//     {
//         m_connection.Close();
//     }
//
//     public async ValueTask<Result> AddToQueue(string name, string url, bool persistent)
//     {
//         var checkDb = new Func<string, Result>((query) =>
//         {   
//             using (var command = new SqliteCommand(query, m_connection))
//             {
//                 command.Parameters.AddWithValue("$name", name);
//                 command.Parameters.AddWithValue("$url", url);
//                 var reader = command.ExecuteReader();
//                 if (reader.HasRows)
//                 {
//                     reader.Read();
//                     string readerName = reader.GetString(reader.GetOrdinal("name")); 
//                     if (readerName == name)
//                     {
//                         return Result.MakeFail("Name already exists!");
//                     }
//                 
//                     return Result.MakeFail($"URL already exists under name {readerName}!");
//                 }
//             }
//             
//             return Result.MakeSuccess();
//         });
//         
//         const string existsQueueQuery = """
//                                         SELECT * FROM Queue 
//                                         WHERE name = $name OR url = $url 
//                                         """;
//         Result queueResult = checkDb(existsQueueQuery);
//         if (!queueResult)
//         {
//             return queueResult;
//         }
//         
//         const string existsLinksQuery = """
//                                         SELECT * FROM Links 
//                                         WHERE name = $name OR url = $url 
//                                         """;
//         Result linksResult = checkDb(existsQueueQuery);
//         if (!linksResult)
//         {
//             return linksResult;
//         }
//
//         string queuePath = $@"{Config.Current.QueueFolderPath}\{name}.json"; 
//         File.Create(queuePath);
//         await File.WriteAllTextAsync(queuePath, JsonConvert.SerializeObject(new QueueEntry
//         {
//             Name = name,
//             Url = url,
//             Persistent = persistent,
//         }));
//
//         const int delayAmount = 500;
//         int delayedTime = 0;
//         const int timeout = 5000;
//         while (true)
//         {
//             delayedTime += delayAmount;
//             if (delayedTime >= timeout)
//             {
//                 return Result.MakeFail("Failed to add to queue!");
//             }
//             await Task.Delay(delayAmount);
//             const string checkQueueQuery = """
//                                            SELECT * FROM Queue WHERE name = $name
//                                            """;
//             using var command = new SqliteCommand(checkQueueQuery, m_connection);
//             command.Parameters.AddWithValue("$name", name);
//             var output = command.ExecuteScalar();
//             if (output != null)
//             {
//                 break;
//             }
//         }
//         return Result.MakeSuccess();
//     }
//     
//     public LinkEntry[] GetLinks(string nameQuery)
//     {
//         List<LinkEntry> entries = new();
//         const string query = """
//                              SELECT * FROM Links WHERE name LIKE N'%$name%'
//                              """;
//         using var command = new SqliteCommand(query, m_connection);
//         command.Parameters.AddWithValue($"name", nameQuery);
//         SqliteDataReader reader = command.ExecuteReader();
//         while (reader.Read())
//         {
//             var entry = new LinkEntry
//             {
//                 Name = reader.GetString(reader.GetOrdinal("url")),
//                 Url = reader.GetString(reader.GetOrdinal("url")),
//                 Persistent = reader.GetBoolean(reader.GetOrdinal("persistent")),
//                 DeleteAfter = reader.GetDateTime(reader.GetOrdinal("delete_after")),
//             };
//
//             ;
//             entries.Add(entry);
//         }
//
//         return entries.ToArray();
//     }
//     
// }