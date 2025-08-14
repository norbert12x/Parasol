using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ParasolBackEnd.Models;
using RestSharp;

namespace ParasolBackEnd.Services
{
    public class OrganizacjaService
    {
        private readonly string _dataFolder;
        private readonly string _apiKey;

        // SemaphoreSlim - pozwala na 2 jednoczesne zapytania
        private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(2, 2);
        private static readonly TimeSpan _throttleDelay = TimeSpan.FromSeconds(1);

        public OrganizacjaService(string dataFolder, string apiKey)
        {
            _dataFolder = dataFolder;
            _apiKey = apiKey;
        }

        public async Task<Organizacja?> WczytajOrganizacjeAsync(string krs)
        {
            // Czekamy na dostęp do "slotu" zapytania
            await _throttle.WaitAsync();
            try
            {
                // Małe opóźnienie, żeby nie przekroczyć limitu API
                await Task.Delay(_throttleDelay);

                var path = Path.Combine(_dataFolder, $"{krs}.json");
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json)) return null;

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

                Console.WriteLine("Pełny adres do geolokalizacji:");
                Console.WriteLine(pelnyAdres);

                var client = new RestClient($"https://eu1.locationiq.com/v1/search?key={_apiKey}");
                var request = new RestRequest($"?q={Uri.EscapeDataString(pelnyAdres)}&format=json");
                request.AddHeader("accept", "application/json");

                var response = await client.GetAsync(request);

                List<Koordynaty> geolokalizacja = new();

                if (!string.IsNullOrWhiteSpace(response?.Content))
                {
                    try
                    {
                        var results = JsonSerializer.Deserialize<JsonElement[]>(response.Content);

                        if (results != null && results.Length > 0)
                        {
                            var first = results[0];
                            geolokalizacja.Add(new Koordynaty
                            {
                                Latitude = double.Parse(first.GetProperty("lat").GetString() ?? "0", CultureInfo.InvariantCulture),
                                Longitude = double.Parse(first.GetProperty("lon").GetString() ?? "0", CultureInfo.InvariantCulture)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Błąd parsowania LocationIQ: {ex.Message}");
                    }
                }

                return new Organizacja
                {
                    Nazwa = nazwa ?? "Brak nazwy",
                    Adresy = adresy,
                    Geolokalizacja = geolokalizacja
                };
            }
            finally
            {
                _throttle.Release();
            }
        }
    }
}
