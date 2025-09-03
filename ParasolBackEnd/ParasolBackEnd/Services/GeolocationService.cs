using System.Text.Json;
using System.Text.Json.Nodes;
using RestSharp;
using ParasolBackEnd.Models.MapOrganizations;
using ParasolBackEnd.DTOs;

namespace ParasolBackEnd.Services
{
    /// Serwis odpowiedzialny za geolokalizację organizacji.
    public class GeolocationService : IGeolocationService
    {
        private readonly string _apiKey;
        private readonly string _dataDirectory;

        public GeolocationService(string apiKey, string dataDirectory = "dane")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        /// Pobiera współrzędne geograficzne dla podanego adresu używając LocationIQ API.
        public async Task<Koordynaty?> GetCoordinatesAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd parsowania LocationIQ dla adresu '{address}': {ex.Message}");
            }

            return null;
        }

        /// Pobiera współrzędne geograficzne dla listy adresów z ograniczeniem prędkości.
        public async Task<List<Koordynaty?>> GetCoordinatesBatchAsync(List<string> addresses)
        {
            if (addresses == null || addresses.Count == 0)
                return new List<Koordynaty?>();

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

        /// Pobiera organizacje według lokalizacji z plików JSON.
        public async Task<List<GeolocationEntityDto>> GetEntitiesByLocationAsync(string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null)
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Console.WriteLine($"Data directory not found: {_dataDirectory}");
                return new List<GeolocationEntityDto>();
            }

            var jsonFiles = Directory.GetFiles(_dataDirectory, "*.json");
            Console.WriteLine($"Found {jsonFiles.Length} JSON files to process for location search");

            var processingTasks = jsonFiles.Select(async filePath => 
                await ProcessFileAsync(filePath, wojewodztwo, powiat, gmina, miejscowosc));
            
            var results = await Task.WhenAll(processingTasks);
            
            return results.Where(r => r != null).ToList();
        }

        /// Przetwarza pojedynczy plik JSON organizacji.
        private async Task<GeolocationEntityDto?> ProcessFileAsync(string filePath, string? wojewodztwo, string? powiat, string? gmina, string? miejscowosc)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                return null;
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    return null;
                }

                var doc = JsonNode.Parse(jsonContent);

                var krsNumber = fileName;
                var name = doc?["odpis"]?["dane"]?["dzial1"]?["danePodmiotu"]?["nazwa"]?.GetValue<string>() ?? "Brak nazwy";

                var address = ExtractAddress(doc);

                if (!MatchesLocationFilter(address, wojewodztwo, powiat, gmina, miejscowosc))
                {
                    return null;
                }

                var allDescriptions = ExtractActivityDescriptions(doc);

                if (!allDescriptions.Any())
                {
                    allDescriptions.Add("Brak opisu działalności");
                }

                var entity = new GeolocationEntityDto
                {
                    KrsNumber = krsNumber,
                    Name = name,
                    ActivityDescriptions = allDescriptions.ToArray(),
                    Address = address
                };

                Console.WriteLine($"Found entity by location: {krsNumber} - {name}");
                return entity;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                return null;
            }
        }

        /// Synchroniczna wersja GetEntitiesByLocationAsync dla kompatybilności wstecznej.
        public IEnumerable<GeolocationEntityDto> GetEntitiesByLocation(string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null)
        {
            return GetEntitiesByLocationAsync(wojewodztwo, powiat, gmina, miejscowosc).GetAwaiter().GetResult();
        }

        /// Ekstrahuje informacje o adresie z dokumentu JSON organizacji.
        private GeolocationAddressDto? ExtractAddress(JsonNode? doc)
        {
            try
            {
                var siedzibaIAdres = doc?["odpis"]?["dane"]?["dzial1"]?["siedzibaIAdres"];
                if (siedzibaIAdres == null) return null;

                var siedziba = siedzibaIAdres["siedziba"];
                var adres = siedzibaIAdres["adres"];

                if (siedziba == null || adres == null) return null;

                return new GeolocationAddressDto
                {
                    Wojewodztwo = siedziba["wojewodztwo"]?.GetValue<string>() ?? "",
                    Powiat = siedziba["powiat"]?.GetValue<string>() ?? "",
                    Gmina = siedziba["gmina"]?.GetValue<string>() ?? "",
                    Miejscowosc = adres["miejscowosc"]?.GetValue<string>() ?? "",
                    Ulica = adres["ulica"]?.GetValue<string>() ?? "",
                    NrDomu = adres["nrDomu"]?.GetValue<string>() ?? "",
                    NrLokalu = adres["nrLokalu"]?.GetValue<string>(),
                    KodPocztowy = adres["kodPocztowy"]?.GetValue<string>() ?? "",
                    Poczta = adres["poczta"]?.GetValue<string>()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting address: {ex.Message}");
                return null;
            }
        }

        /// Ekstrahuje opisy działalności organizacji z dokumentu JSON.
        private List<string> ExtractActivityDescriptions(JsonNode? doc)
        {
            var allDescriptions = new List<string>();

            // Metoda 1: celDzialaniaOrganizacji
            var celDzialania = doc?["odpis"]?["dane"]?["dzial3"]?["celDzialaniaOrganizacji"]?["celDzialania"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(celDzialania))
            {
                allDescriptions.Add(celDzialania);
            }

            // Metoda 2: przedmiotDzialalnosciOPP.nieodplatnyPkd[].opis
            var pkdNode = doc?["odpis"]?["dane"]?["dzial3"]?["przedmiotDzialalnosciOPP"]?["nieodplatnyPkd"];
            if (pkdNode != null && pkdNode is JsonArray pkdArray)
            {
                foreach (var pkdItem in pkdArray)
                {
                    var opis = pkdItem?["opis"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(opis))
                    {
                        allDescriptions.Add(opis);
                    }
                }
            }

            // Metoda 3: inne pola opisowe
            var otherActivityFields = new[] { "przedmiotDzialalnosci", "działalność", "cel", "opis" };
            foreach (var field in otherActivityFields)
            {
                var value = doc?["odpis"]?["dane"]?["dzial3"]?[field]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    allDescriptions.Add(value);
                }
            }

            return allDescriptions;
        }

        /// Sprawdza czy adres organizacji pasuje do podanych filtrów lokalizacji.
        private bool MatchesLocationFilter(GeolocationAddressDto? address, string? wojewodztwo, string? powiat, string? gmina, string? miejscowosc)
        {
            if (address == null) return false;

            if (!string.IsNullOrWhiteSpace(wojewodztwo) &&
                !address.Wojewodztwo.Contains(wojewodztwo, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(powiat) &&
                !address.Powiat.Contains(powiat, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(gmina) &&
                !address.Gmina.Contains(gmina, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(miejscowosc) &&
                !address.Miejscowosc.Contains(miejscowosc, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
