using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using RoWifi_Alpha.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class RobloxService
    {
        private readonly HttpClient _client;
        private readonly IMemoryCache _cache;

        public RobloxService(HttpClient client, IMemoryCache cache)
        {
            _client = client;
            _cache = cache;
        }
        
        public async Task<int?> GetIdFromUsername(string Username)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(new Uri($"http://api.roblox.com/users/get-by-username?username={Username}"));
                string res = await response.Content.ReadAsStringAsync();
                JObject obj = JObject.Parse(res);
                return (int?)obj["Id"];
            }
            catch (Exception e)
            {
                throw new RobloxException(e.Message);
            }
        }

        public async Task<bool> CheckCode(int RobloxId, string code)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(new Uri($"https://www.roblox.com/users/{RobloxId}/profile"));
                string res = await response.Content.ReadAsStringAsync();
                bool IsPresent = res.Contains(code, StringComparison.OrdinalIgnoreCase);
                return IsPresent;
            }
            catch (Exception e)
            {
                throw new RobloxException(e.Message);
            }
        }

        public async Task<Dictionary<int, int>> GetUserRoles(int RobloxId)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(new Uri($"https://groups.roblox.com/v2/users/{RobloxId}/groups/roles"));
                response.EnsureSuccessStatusCode();
                string result = await response.Content.ReadAsStringAsync();
                JObject obj = JObject.Parse(result);
                Dictionary<int, int> roleIds = new Dictionary<int, int>();
                foreach (var group in obj["data"])
                {
                    int role = (int)group["role"]["rank"];
                    int groupId = (int)group["group"]["id"];
                    roleIds.Add(groupId, role);
                }
                return roleIds;
            }
            catch(Exception e)
            {
                throw new RobloxException(e.Message);
            }
        }

        public async Task<string> GetUsernameFromId(int RobloxId)
        {
            try
            {
                if (!_cache.TryGetValue(RobloxId, out string Username))
                {
                    HttpResponseMessage response = await _client.GetAsync(new Uri($"https://api.roblox.com/users/{RobloxId}"));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    JObject obj = JObject.Parse(result);
                    Username = (string)obj["Username"];
                    if (Username != null && Username.Length > 0)
                    {
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromHours(6));
                        _cache.Set(RobloxId, Username);
                    }
                }
                return Username;
            }
            catch(Exception e)
            {
                throw new RobloxException(e.Message);
            }
        }

        public async Task<JToken> GetGroupRank(int GroupId, int RankId)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(new Uri($"https://groups.roblox.com/v1/groups/{GroupId}/roles"));
                string res = await response.Content.ReadAsStringAsync();
                JObject obj = JObject.Parse(res);
                JArray ranks = (JArray)obj["roles"];
                return ranks.Where(r => (int)r["rank"] == RankId).FirstOrDefault();
            }
            catch(Exception e)
            {
                throw new RobloxException(e.Message);
            }
        }

        public async Task<List<JToken>> GetGroupRolesInRange(int RobloxId, int Min, int Max)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(new Uri($"https://groups.roblox.com/v1/groups/{RobloxId}/roles"));
                string result = await response.Content.ReadAsStringAsync();
                JObject jobject = JObject.Parse(result);
                JArray roles = (JArray)jobject["roles"];
                var req = roles.Where(r => (int)r["rank"] >= Min && (int)r["rank"] <= Max).Distinct().ToList();
                return req;
            }
            catch (Exception e)
            {
                throw new RobloxException(e.Message);
            }
        }
    }
}
