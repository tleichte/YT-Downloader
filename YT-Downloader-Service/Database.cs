// using System.Data;
// using Microsoft.Data.Sqlite;
//
// namespace YT_Downloader_Service;
//
// public class Database
// {
//     public static Database Instance { get; private set; }
//     static Database()
//     {
//         Instance = new Database(Config.Current.DatabasePath);
//         Instance.InitializeDB();
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
//     private void InitializeDB()
//     {
//
//         const string createQueryTableCommand = """
//                                                CREATE TABLE IF NOT EXISTS "Queue" (
//                                                    name TEXT PRIMARY KEY,
//                                                    url TEXT,
//                                                    disk_url TEXT,
//                                                    persistent INTEGER,
//                                                    status TEXT
//                                                )
//                                                """;
//         using (var command = new SqliteCommand(createQueryTableCommand, m_connection))
//         {
//             command.ExecuteNonQuery();
//         }
//         
//         const string createLinksTableCommand = """
//                                                CREATE TABLE IF NOT EXISTS "Links" (
//                                                    name TEXT PRIMARY KEY,
//                                                    url TEXT,
//                                                    persistent INTEGER,
//                                                    delete_after INTEGER
//                                                )
//                                                """;
//     }
//
//     public void AddLink(LinkEntry entry)
//     {
//         const string addCommand = """
//                                   INSERT INTO "Links" (name, url, disk_url, persistent, delete_after) values ($name, $url, $disk_url, $persistent, null)
//                                   """;
//         using var command = new SqliteCommand(addCommand, m_connection);
//         
//         command.Parameters.AddWithValue("$name",  entry.Name);
//         command.Parameters.AddWithValue("$url", entry.Url);
//         command.Parameters.AddWithValue("$disk_url", entry.DiskUrl);
//         command.Parameters.AddWithValue("$persistent", entry.Persistent);
//         command.ExecuteNonQuery();
//     }
//     
//
//     public async Task RunDeleteAfters()
//     {
//         List<string> deletionNames = new();
//         
//         const string selectCommand = """
//                                         SELECT * FROM "Links" 
//                                         WHERE persistent = FALSE AND delete_after > $currentDate
//                                      """;
//         await using (var command = new SqliteCommand(selectCommand, m_connection))
//         {
//             command.Parameters.AddWithValue("$currentDate", DateTime.Now);
//             var reader = await command.ExecuteReaderAsync();
//             
//             while (await reader.ReadAsync())
//             {
//                 string name = reader.GetString(reader.GetOrdinal("name"));
//                 deletionNames.Add(name);
//
//                 string diskUrl = reader.GetString(reader.GetOrdinal("disk_url"));
//                 File.Delete(diskUrl);
//             }
//         }
//         
//         const string deleteCommand = """
//                                      DELETE FROM "Links" WHERE name = $name
//                                      """;
//         foreach (var name in deletionNames)
//         {
//             await using var command = new SqliteCommand(deleteCommand, m_connection);
//             command.Parameters.AddWithValue("$name", name);
//             await command.ExecuteNonQueryAsync();
//         }
//     }
//
//     public Result GetFirstQueueEntry(out QueueEntry queueEntry)
//     {
//         const string selectCommand = """
//                                         SELECT * FROM "Queue" LIMIT 1;
//                                      """;
//         using var command = new SqliteCommand(selectCommand, m_connection);
//         SqliteDataReader reader = command.ExecuteReader();
//         if (reader.Read())
//         {
//             queueEntry = new QueueEntry
//             {
//                 Name = reader.GetString(reader.GetOrdinal("name")),
//                 Url = reader.GetString(reader.GetOrdinal("url")),
//                 Persistent = reader.GetBoolean(reader.GetOrdinal("persistent")),
//             };
//             return Result.MakeSuccess();
//         }
//
//         queueEntry = default;
//         return Result.MakeFail("Nothing in the queue!");
//     }
// }