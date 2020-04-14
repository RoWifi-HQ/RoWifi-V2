using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace RoWifi_Alpha.Utilities
{
    public class PatreonService
    {
        private readonly HttpClient _client;

        public PatreonService(HttpClient client)
        {
            _client = client;
            _client.DefaultRequestHeaders.Add("Authorization", Environment.GetEnvironmentVariable("PATREON"));
        }

        public async Task<(string, int?)> GetPatron(string DiscordId)
        {
            string link = "https://www.patreon.com/api/oauth2/api/campaigns/3229705/pledges?include=patron.null";
            while (link != null)
            {
                HttpResponseMessage response = await _client.GetAsync(new Uri(link));
                string res = await response.Content.ReadAsStringAsync();
                JObject obj = JObject.Parse(res);
                JArray Info = (JArray)obj["included"];
                JArray Users = (JArray)obj["data"];
                foreach (JToken user in Info)
                {
                    if (user["type"].ToString() == "user")
                    {
                        JToken disc = user["attributes"]["social_connections"]["discord"];
                        string PatreonId = user["id"].ToString();
                        if (disc.ToString().Length > 0)
                        {
                            if (disc["user_id"].ToString() == DiscordId)
                            {
                                foreach (JToken U in Users)
                                {
                                    if (U["relationships"]["patron"]["data"]["id"].ToString() == PatreonId)
                                    {
                                        JToken Reward = U["relationships"]["reward"];
                                        if (Reward != null)
                                        {
                                            JToken Tier = Reward["data"]["id"];
                                            return (PatreonId, (int)Tier);
                                        }
                                        return (PatreonId, null);
                                    }
                                }
                            }
                        }
                    }
                }
                link = obj["links"]["next"]?.ToString();
            }
            return ("None", null);
        }
    }
}
