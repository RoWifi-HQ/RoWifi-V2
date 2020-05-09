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
            string link = "https://www.patreon.com/api/oauth2/v2/campaigns/3229705/members?include=currently_entitled_tiers,user&fields%5Buser%5D=social_connections";
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
                        JToken disc = user["attributes"]?["social_connections"]?["discord"] ?? "";
                        if (disc.ToString().Length > 0 && disc["user_id"].ToString() == DiscordId)
                        {
                            string PatreonId = user["id"].ToString();
                            foreach (JToken U in Users)
                            {
                                if (U["relationships"]["user"]["data"]["id"].ToString() == PatreonId)
                                {
                                    var Tiers = (JArray)U["relationships"]["currently_entitled_tiers"]["data"];
                                    if (Tiers.Count > 0)
                                        return (PatreonId, (int)Tiers[0]["id"]);
                                    return (PatreonId, null);
                                }
                            }
                        }
                    }
                }
                link = obj["links"]?["next"]?.ToString();
            }
            return ("None", null);
        }
    }
}
