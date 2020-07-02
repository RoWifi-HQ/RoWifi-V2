using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;
using RoWifi_Alpha.Exceptions;
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
        private readonly IMongoCollection<RoGuild> _guilds;
        private readonly IMongoCollection<Premium> _premium;
        private readonly IMongoCollection<RoBackup> _backups;
        private readonly IMongoCollection<QueueUser> _queue;
        private readonly IMemoryCache _cache;

        public DatabaseService(IMemoryCache cache)
        {
            _client = new MongoClient(Environment.GetEnvironmentVariable("DBConn"));
            _database = _client.GetDatabase("RoWifi");
            _users = _database.GetCollection<RoUser>("users");
            _guilds = _database.GetCollection<RoGuild>("guilds");
            _premium = _database.GetCollection<Premium>("premium");
            _backups = _database.GetCollection<RoBackup>("backups");
            _queue = _database.GetCollection<QueueUser>("queue");
            _cache = cache;
        }

        public async Task<RoUser> GetUserAsync(ulong DiscordId)
        {
            try
            {
                if (!_cache.TryGetValue(DiscordId, out RoUser user))
                {
                    IAsyncCursor<RoUser> cursor = await _users.FindAsync(u => u.DiscordId == DiscordId);
                    user = cursor.FirstOrDefault();
                    if (user != null)
                    {
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                            .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
                        _cache.Set(DiscordId, user, cacheOptions);
                    }
                }
                return user;
            }
            catch(Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<IEnumerable<RoUser>> GetUsersAsync(IEnumerable<ulong> DiscordIds)
        {
            try
            {
                FilterDefinition<RoUser> filter = Builders<RoUser>.Filter.In(u => u.DiscordId, DiscordIds);
                IAsyncCursor<RoUser> cursor = await _users.FindAsync(filter);
                return cursor.ToEnumerable();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message); ;
            }
        }

        public async Task AddUser(RoUser user, bool newUser = true)
        {
            try
            {
                if (newUser)
                    await _users.InsertOneAsync(user);
                else
                    await _users.ReplaceOneAsync(u => u.DiscordId == user.DiscordId, user);
                var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
                _cache.Set(user.DiscordId, user, cacheOptions);
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<QueueUser> GetQueueUser(int RobloxId)
        {
            try
            {
                IAsyncCursor<QueueUser> cursor = await _queue.FindAsync(b => b.RobloxId == RobloxId);
                return cursor.FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task AddQueueUser(QueueUser user)
        {
            try
            {
                if (await GetQueueUser(user.RobloxId) == null)
                    await _queue.InsertOneAsync(user);
                else
                    await _queue.FindOneAndReplaceAsync(f => f.RobloxId == user.RobloxId, user);
                _cache.Remove(user.DiscordId);
            }
            catch(Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<bool> AddGuild(RoGuild guild, bool newGuild)
        {
            try
            {
                if (newGuild)
                {
                    await _guilds.InsertOneAsync(guild);
                }
                else
                {
                    await _guilds.ReplaceOneAsync(g => g.GuildId == guild.GuildId, guild);
                }
                var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                _cache.Set(guild.GuildId, guild, cacheOptions);
                return true;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<RoGuild> GetGuild(ulong GuildId)
        {
            try
            {
                if (!_cache.TryGetValue(GuildId, out RoGuild guild))
                {
                    IAsyncCursor<RoGuild> cursor = await _guilds.FindAsync(u => u.GuildId == GuildId);
                    guild = cursor.FirstOrDefault();
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));
                    _cache.Set(GuildId, guild, cacheOptions);
                }
                return guild;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<IEnumerable<RoGuild>> GetGuilds(IEnumerable<ulong> GuildsIds, bool PremiumOnly = false)
        {
            try
            {
                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.In(g => g.GuildId, GuildsIds);
                if (PremiumOnly) filter &= Builders<RoGuild>.Filter.Where(g => g.Settings.AutoDetection);
                IAsyncCursor<RoGuild> cursor = await _guilds.FindAsync(filter);
                return cursor.ToEnumerable();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<bool> ModifyGuild(ulong GuildId, UpdateDefinition<RoGuild> update, FilterDefinition<RoGuild> filter = null)
        {
            try
            {
                if (filter == null)
                    filter = Builders<RoGuild>.Filter.Where(g => g.GuildId == GuildId);
                var options = new FindOneAndUpdateOptions<RoGuild, RoGuild> { ReturnDocument = ReturnDocument.After };
                RoGuild guild = await _guilds.FindOneAndUpdateAsync(filter, update, options);
                var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                _cache.Set(GuildId, guild, cacheOptions);
                return true;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<bool> AddPremium(Premium premium)
        {
            try
            {
                await _premium.InsertOneAsync(premium);
                return true;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<Premium> GetPremium(ulong DiscordId)
        {
            try
            {
                IAsyncCursor<Premium> cursor = await _premium.FindAsync(b => b.DiscordId == DiscordId);
                return cursor.FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<List<Premium>> GetAllPremium()
        {
            try
            {
                IAsyncCursor<Premium> cursor = await _premium.FindAsync(b => b.PatreonId != 0);
                return cursor.ToList();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<bool> ModifyPremium(ulong DiscordId, UpdateDefinition<Premium> update)
        {
            try
            {
                await _premium.FindOneAndUpdateAsync(u => u.DiscordId == DiscordId, update);
                return true;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<bool> DeletePremium(ulong DiscordId)
        {
            try
            {
                await _premium.DeleteOneAsync(p => p.DiscordId == DiscordId);
                return true;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<RoBackup> GetBackup(ulong UserId, string Name)
        {
            try
            {
                IAsyncCursor<RoBackup> cursor = await _backups.FindAsync(b => b.UserId == UserId && b.Name == Name);
                return cursor.FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<List<RoBackup>> GetBackups(ulong UserId)
        {
            try
            {
                IAsyncCursor<RoBackup> cursor = await _backups.FindAsync(b => b.UserId == UserId);
                return cursor.ToList();
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<bool> AddBackup(RoBackup backup, string Name)
        {
            try
            {
                if (await GetBackup(backup.UserId, backup.Name) == null)
                    await _backups.InsertOneAsync(backup);
                else
                    await _backups.FindOneAndReplaceAsync(b => b.UserId == backup.UserId && b.Name == Name, backup);
                return true;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }

        public async Task<Dictionary<ulong, string>> GetPrefixes()
        {
            try
            {
                FilterDefinition<RoGuild> filter = Builders<RoGuild>.Filter.Empty;
                ProjectionDefinition<RoGuild> projection = Builders<RoGuild>.Projection.Include(g => g.CommandPrefix);
                FindOptions<RoGuild, BsonDocument> options = new FindOptions<RoGuild, BsonDocument> { Projection = projection };
                Dictionary<ulong, string> Prefixes = new Dictionary<ulong, string>();

                using (var cursor = await _guilds.FindAsync(filter, options))
                {
                    while (await cursor.MoveNextAsync())
                    {
                        var batch = cursor.Current;
                        foreach (BsonDocument b in batch)
                            Prefixes.Add((ulong)b["_id"].ToInt64(), b.GetValue("Prefix", "!").AsString);
                    }
                }
                return Prefixes;
            }
            catch (Exception e)
            {
                throw new RoMongoException(e.Message);
            }
        }
    }
}
