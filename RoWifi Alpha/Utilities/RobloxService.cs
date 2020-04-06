using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class RobloxService
    {
        private readonly HttpClient _client;

        public RobloxService(HttpClient client)
        {
            _client = client;
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
            catch (Exception)
            {
                throw;
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
            catch (Exception)
            {
                throw;
            }
        }
    }
}
