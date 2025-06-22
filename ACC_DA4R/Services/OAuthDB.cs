using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace ACC_DA4R.Services
{
    public static class OAuthDB
    {
        private static MongoClient _client = null;
        private static IMongoDatabase _database = null;
        private static string ConnectionString => Credentials.GetAppSetting("OAUTH_DATABASE");

        private static string OAuthDatabase { get { return Credentials.GetAppSetting("OAUTH_DATABASE"); } }

        private static MongoClient Client
        {
            get
            {
                if (_client == null) _client = new MongoClient(OAuthDatabase);
                return _client;
            }
        }


        public static IMongoDatabase Database
        {
            get
            {
                if (_database == null)
                {
                    var mongoUrl = new MongoUrl(ConnectionString);
                    string dbName = mongoUrl.DatabaseName;

                    if (string.IsNullOrWhiteSpace(dbName))
                        throw new Exception("MongoDB connection string is missing a database name.");

                    _database = Client.GetDatabase(dbName);
                }
                return _database;
            }
        }

        public async static Task<bool> Register(string userId, string credentials)
        {
            var users = Database.GetCollection<BsonDocument>("users");
            if (await IsRegistered(userId))
            {
                JObject credentialsJson = JObject.Parse(credentials);
                var filterBuilder = Builders<BsonDocument>.Filter;
                var filter = filterBuilder.Eq("_id", userId);
                var update = Builders<BsonDocument>.Update
                                    .Set("TokenInternal", (string)credentialsJson["TokenInternal"])
                                    .Set("TokenPublic", (string)credentialsJson["TokenPublic"])
                                    .Set("RefreshToken", (string)credentialsJson["RefreshToken"])
                                    .Set("ExpiresAt", ((DateTime)credentialsJson["ExpiresAt"]).ToString("O"));

                try { var result = await users.UpdateOneAsync(filter, update); }
                catch (Exception e) { Console.WriteLine(e); return false; }
            }
            else
            {
                var document = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(credentials);
                document["_id"] = userId;
                try { await users.InsertOneAsync(document); }
                catch (Exception e) { Console.WriteLine(e); return false; }
            }

            return true;
        }

        public static async Task<bool> IsRegistered(string userId)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq("_id", userId);
            var users = Database.GetCollection<BsonDocument>("users");
            try { long count = await users.CountDocumentsAsync(filter); return (count == 1); }
            catch (Exception e) { Console.WriteLine(e); return false; }
        }

        public static async Task<BsonDocument> GetCredentials(string userId)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq("_id", userId);
            var users = Database.GetCollection<BsonDocument>("users");
            try
            {
                var docs = await users.FindAsync(filter);
                var doc = docs.First();
                return doc;
            }
            catch (Exception e) { Console.WriteLine(e); return null; }
        }
    }
}
