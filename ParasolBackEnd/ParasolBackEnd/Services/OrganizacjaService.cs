using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ParasolBackEnd.Models.MapOrganizations;
using RestSharp;
using System.Linq; // Added for FirstOrDefault

namespace ParasolBackEnd.Services
{
    /// <summary>
    /// Serwis do wczytywania organizacji z plików JSON i pobierania ich geolokalizacji.
    /// </summary>
    public class OrganizacjaService
    {
        private readonly string _dataFolder;
        private readonly string _apiKey;
        private readonly ILogger<OrganizacjaService> _logger;

        // Throttling dla LocationIQ API - max 2 zapytania na sekundę
        private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(1, 1); // 1 zapytanie naraz
        private static readonly TimeSpan _throttleDelay = TimeSpan.FromMilliseconds(600); // ~2 req/s (bezpieczny margines)

        public OrganizacjaService(string dataFolder, string apiKey, ILogger<OrganizacjaService> logger)
        {
            _dataFolder = dataFolder ?? throw new ArgumentNullException(nameof(dataFolder));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Wczytuje organizację z pliku JSON i pobiera jej geolokalizację.
        /// </summary>
        /// <param name="krs">Numer KRS organizacji</param>
        /// <returns>Organizacja z adresami i koordynatami lub null jeśli nie znaleziono</returns>
        public async Task<Organizacja?> LoadOrganizationAsync(string krs)
        {
            if (string.IsNullOrWhiteSpace(krs))
            {
                _logger.LogWarning("Próba wczytania organizacji z pustym numerem KRS");
                return null;
            }

            _logger.LogDebug("Wczytywanie organizacji KRS: {Krs}", krs);
            
            // Czekamy na dostęp do "slotu" zapytania
            await _throttle.WaitAsync();
            try
            {
                // Małe opóźnienie między wywołaniami, aby nie przekroczyć limitu API
                await Task.Delay(_throttleDelay);

                var path = Path.Combine(_dataFolder, $"{krs}.json");
                if (!File.Exists(path))
                {
                    _logger.LogDebug("Plik JSON nie istnieje dla KRS: {Krs}", krs);
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Pusty plik JSON dla KRS: {Krs}", krs);
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("odpis", out var odpis)) return null;

                var dane = odpis.GetProperty("dane");
                var dzial1 = dane.GetProperty("dzial1");

                var nazwa = dzial1.GetProperty("danePodmiotu")
                                  .GetProperty("nazwa")
                                  .GetString();

                var adresy = new List<Adres>();

                if (dzial1.TryGetProperty("siedzibaIAdres", out var siedzibaIAdres))
                {
                    var siedziba = siedzibaIAdres.GetProperty("siedziba");
                    var adres = siedzibaIAdres.GetProperty("adres");

                    adresy.Add(new Adres
                    {
                        NumerKrs = krs,
                        Ulica = adres.TryGetProperty("ulica", out var ul) ? ul.GetString() ?? "" : "",
                        NrDomu = adres.TryGetProperty("nrDomu", out var nd) ? nd.GetString() ?? "" : "",
                        NrLokalu = adres.TryGetProperty("nrLokalu", out var nl) ? nl.GetString() : null,
                        Miejscowosc = adres.TryGetProperty("miejscowosc", out var mj) ? mj.GetString() ?? "" : "",
                        KodPocztowy = adres.TryGetProperty("kodPocztowy", out var kp) ? kp.GetString() ?? "" : "",
                        Poczta = adres.TryGetProperty("poczta", out var pcz) ? pcz.GetString() : null,
                        Gmina = siedziba.TryGetProperty("gmina", out var gm) ? gm.GetString() ?? "" : "",
                        Powiat = siedziba.TryGetProperty("powiat", out var pw) ? pw.GetString() ?? "" : "",
                        Wojewodztwo = siedziba.TryGetProperty("wojewodztwo", out var wj) ? wj.GetString() ?? "" : "",
                        Kraj = adres.TryGetProperty("kraj", out var kr) ? kr.GetString() ?? "" : ""
                    });
                }

                string pelnyAdres = string.Join(", ", adresy.ConvertAll(a =>
                    $"{a.Ulica} {a.NrDomu}{(a.NrLokalu != null ? "/" + a.NrLokalu : "")}, {a.Miejscowosc}, {a.KodPocztowy}, {a.Wojewodztwo}, {a.Kraj}"));

                _logger.LogDebug("Adres do geolokalizacji KRS {Krs}: {Adres}", krs, pelnyAdres);

                var client = new RestClient($"https://eu1.locationiq.com/v1/search?key={_apiKey}");
                var request = new RestRequest($"?q={Uri.EscapeDataString(pelnyAdres)}&format=json");
                request.AddHeader("accept", "application/json");

                List<Koordynaty> geolokalizacja = new();

                // Retry/backoff dla 429 i 5xx
                var maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        _logger.LogDebug("Wysyłanie zapytania do LocationIQ (attempt {Attempt}) dla KRS: {Krs}", attempt, krs);
                        var response = await client.GetAsync(request);

                        if (response != null && response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                        {
                            try
                            {
                                var results = JsonSerializer.Deserialize<JsonElement[]>(response.Content);
                                if (results != null && results.Length > 0)
                                {
                                    var first = results[0];
                                    var latitude = double.Parse(first.GetProperty("lat").GetString() ?? "0", CultureInfo.InvariantCulture);
                                    var longitude = double.Parse(first.GetProperty("lon").GetString() ?? "0", CultureInfo.InvariantCulture);
                                    geolokalizacja.Add(new Koordynaty { NumerKrs = krs, Latitude = latitude, Longitude = longitude });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Błąd parsowania odpowiedzi LocationIQ dla KRS: {Krs}", krs);
                            }
                            break; // sukces, wychodzimy z retry
                        }

                        // Obsługa 429 / 5xx
                        var status = (int?)response?.StatusCode;
                        if (status == 429 || (status >= 500 && status < 600))
                        {
                            TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                            var retryAfter = response?.Headers?.FirstOrDefault(h => string.Equals(h.Name, "Retry-After", StringComparison.OrdinalIgnoreCase)).Value?.ToString();
                            if (!string.IsNullOrWhiteSpace(retryAfter) && int.TryParse(retryAfter, out var retrySec))
                            {
                                delay = TimeSpan.FromSeconds(retrySec);
                            }
                            _logger.LogWarning("LocationIQ limit/awaria (status {Status}) dla KRS {Krs}. Próba {Attempt}/{MaxAttempts}, czekam {Delay}s", status, krs, attempt, maxAttempts, delay.TotalSeconds);
                            await Task.Delay(delay);
                            continue;
                        }

                        // Inne błędy – log i przerwij retry
                        _logger.LogWarning("Nieudane zapytanie do LocationIQ dla KRS {Krs}. Status: {Status}", krs, status);
                        break;
                    }
                    catch (HttpRequestException httpEx)
                    {
                        _logger.LogWarning(httpEx, "Błąd HTTP podczas pobierania geolokalizacji dla KRS {Krs} (attempt {Attempt})", krs, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    }
                }

                var organizacja = new Organizacja
                {
                    NumerKrs = krs,
                    Nazwa = nazwa ?? "Brak nazwy",
                    Adresy = adresy,
                    Koordynaty = geolokalizacja
                };

                _logger.LogInformation("Pomyślnie wczytano organizację KRS: {Krs}, nazwa: {Nazwa}, adresy: {AdresyCount}, koordynaty: {KoordynatyCount}", 
                    krs, organizacja.Nazwa, adresy.Count, geolokalizacja.Count);

                return organizacja;
            }
            finally
            {
                _throttle.Release();
            }
        }
    }
}
