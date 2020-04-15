using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;

        public DatabaseService(IMemoryCache cache)
        {
            _client = new MongoClient(Environment.GetEnvironmentVariable("DBConn"));
            _database = _client.GetDatabase("RoWifi");
            _users = _database.GetCollection<RoUser>("users");
            _guilds = _database.GetCollection<RoGuild>("guilds");
            _premium = _database.GetCollection<Premium>("premium");
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
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                    _cache.Set(DiscordId, user, cacheOptions);
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
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
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
                RoGuild guild = await _guilds.FindOneAndUpdateAsync(filter, update);
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
    }
}
