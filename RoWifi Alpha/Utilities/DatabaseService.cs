using MongoDB.Driver;
using RoWifi_Alpha.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class DatabaseService
    {
        private readonly IMongoClient _client;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<RoUser> _users;

        public DatabaseService()
        {
            _client = new MongoClient(Environment.GetEnvironmentVariable("DBConn"));
            _database = _client.GetDatabase("RoWifi");
            _users = _database.GetCollection<RoUser>("users");
        }

        public async Task<RoUser> GetUserAsync(ulong DiscordId)
        {
            IEnumerable<RoUser> users = await GetUsersAsync(new List<ulong>() { DiscordId });
            return users.FirstOrDefault();
        }

        public async Task<IEnumerable<RoUser>> GetUsersAsync(IEnumerable<ulong> DiscordIds)
        {
            try
            {
                FilterDefinition<RoUser> filter = Builders<RoUser>.Filter.In(u => u.DiscordId, DiscordIds);
                IAsyncCursor<RoUser> cursor = await _users.FindAsync(filter);
                return cursor.ToEnumerable();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> AddUser(RoUser user, bool newUser = true)
        {
            try
            {
                if (newUser)
                    await _users.InsertOneAsync(user);
                else
                    await _users.ReplaceOneAsync(u => u.DiscordId == user.DiscordId, user);
                return true;
            }
            catch (Exception)
            {
                //Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}
