using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamLibrary.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamLibrary.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SteamController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly long _steamId;
        public List<Game> games;

        public SteamController(IConfiguration configuration)
        {

            _configuration = configuration;
            games = new List<Game>();
            _steamId = _configuration.GetValue<long>("SteamId");

        }

        [HttpGet("SteamLibrary")]
        public async Task<IActionResult> GetSteamLibrary()
        {
            // api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=XXXXXXXXXXXXXXXXX&steamid=76561197960434622&format=json
            long steamID = _steamId;
            string apiKey = _configuration.GetValue<string>("SteamAPIKey");
            using (var httpClient = new HttpClient())
            {
                var fullUrl = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamID.ToString()}&format=json";
                httpClient.BaseAddress = new Uri(fullUrl);
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await httpClient.GetAsync(fullUrl);

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    AssignGameIDToModel(data);
                    await GetGameTitleFromSteam();
                    //PrintGameList();
                    return Content(data, "application/json");
                }
            }
            return Ok();
        }

        [HttpGet("AppList")]
        public async Task<IActionResult> GetSteamAppInfo()
        {
            string apiKey = _configuration.GetValue<string>("SteamAPIKey");
            using (var client = new HttpClient())
            {
                var url = $"http://api.steampowered.com/ISteamApps/GetAppList/v0002/?key={apiKey}&format=json";
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    return Content(data, "application/json");
                }

            }
            return Ok();
        }

        protected void AssignGameIDToModel(string ownedGameData)
        {
            if (string.IsNullOrEmpty(ownedGameData)) {  return; }
            JObject data = JObject.Parse(ownedGameData);
            games.Clear();
            for (int i = 0; i < data["response"]["games"].Count(); i++)
            {
                var item = data["response"]["games"][i];
                games.Add(new Game { Id = i, SteamId = (int)item["appid"] });
            }

        }

        protected void PrintGameList()
        {
            Console.WriteLine("Printing games list...");
            foreach (var game in games)
            {
                Console.WriteLine($"ID: {game.Id} - Steam App ID: {game.SteamId} - Steam Title: {game.Title}");

            }
        }

        protected async Task<IActionResult> GetGameTitleFromSteam()
        {
            if(games.Count == 0 || games == null) { return BadRequest("No games to process"); }
            var tasks = new List<Task>();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                foreach (var game in games)
                {
                    var url = $"https://store.steampowered.com/api/appdetails?appids={game.SteamId}&format=json";
                    tasks.Add(ProcessGameAsync(client, url, game));
                }
                await Task.WhenAll(tasks); 
            }
            return Ok();
        }

        private async Task ProcessGameAsync(HttpClient client, string url, Game game)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    
                    JObject gameData = JObject.Parse(data);
                    Console.WriteLine(gameData);
                    if (gameData[game.SteamId.ToString()] != null && 
                        gameData[game.SteamId.ToString()]["data"] != null && 
                        gameData[game.SteamId.ToString()]["data"]["name"] != null
                       )
                    {
                        game.Title = (string)gameData[game.SteamId.ToString()]["data"]["name"];
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to fetch data for game with Steam ID: {game.SteamId}. Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing game with Steam ID: {game.SteamId}. Exception: {ex.Message}");
            }
        }
    }
}
