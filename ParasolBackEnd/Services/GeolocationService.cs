using System.Text.Json;
using RestSharp;
using ParasolBackEnd.Models;

namespace ParasolBackEnd.Services
{
    public class GeolocationService : IGeolocationService
    {
        private readonly string _apiKey;

        public GeolocationService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<Koordynaty?> GetCoordinatesAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var client = new RestClient($"https://eu1.locationiq.com/v1/search?key={_apiKey}");
            var request = new RestRequest("");
            request.AddParameter("q", address);
            request.AddParameter("format", "json");
            request.AddHeader("accept", "application/json");

            var response = await client.GetAsync(request);

            if (response == null || !response.IsSuccessful)
            {
                Console.WriteLine($"Błąd API dla adresu '{address}': {response?.StatusCode}");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(response.Content))
            {
                try
                {
                    var results = JsonSerializer.Deserialize<JsonElement[]>(response.Content);

                    if (results.Length > 0)
                    {
                        var first = results[0];
                        return new Koordynaty
                        {
                            Latitude = double.Parse(first.GetProperty("lat").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                            Longitude = double.Parse(first.GetProperty("lon").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd parsowania LocationIQ: {ex.Message}");
                }
            }

            return null;
        }

        // Nowa metoda obsługująca wiele adresów z throttlingiem
        public async Task<List<Koordynaty?>> GetCoordinatesBatchAsync(List<string> addresses)
        {
            var geolokalizacje = new List<Koordynaty?>();

            foreach (var address in addresses)
            {
                var coords = await GetCoordinatesAsync(address);
                geolokalizacje.Add(coords);

                // Throttling: max 2 zapytania na sekundę
                await Task.Delay(500);
            }

            return geolokalizacje;
        }
    }
}
