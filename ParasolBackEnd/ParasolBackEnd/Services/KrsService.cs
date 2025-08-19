using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParasolBackEnd.Services
{
    public class KrsEntityDto
    {
        public string KrsNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string[] ActivityDescriptions { get; set; } = Array.Empty<string>();
        public AddressDto? Address { get; set; }
    }

    public class AddressDto
    {
        public string Wojewodztwo { get; set; } = string.Empty;
        public string Powiat { get; set; } = string.Empty;
        public string Gmina { get; set; } = string.Empty;
        public string Miejscowosc { get; set; } = string.Empty;
        public string Ulica { get; set; } = string.Empty;
        public string NrDomu { get; set; } = string.Empty;
        public string? NrLokalu { get; set; }
        public string KodPocztowy { get; set; } = string.Empty;
        public string? Poczta { get; set; }
    }

    public class KrsService
    {
        private readonly string _dataDirectory;

        public KrsService(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
        }

        public IEnumerable<string> GetCategories()
        {
            return new[]
            {
                "edukacja", "szkolnictwo", "oświata",
                "zdrowie", "medycyna", "opieka zdrowotna",
                "kultura", "sztuka", "muzyka", "teatr",
                "sport", "rekreacja", "turystyka",
                "nauka", "badania", "technologia",
                "ekologia", "środowisko", "ochrona przyrody",
                "pomoc społeczna", "charytatywna", "dobroczynność",
                "religia", "kościół", "wiara",
                "gospodarka", "przedsiębiorczość", "biznes"
            };
        }

        public IEnumerable<KrsEntityDto> GetEntities(string[]? krsNumbers = null, string[]? filterCategories = null)
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Console.WriteLine($"Data directory not found: {_dataDirectory}");
                yield break;
            }

            var jsonFiles = Directory.GetFiles(_dataDirectory, "*.json");
            Console.WriteLine($"Found {jsonFiles.Length} JSON files to process");

            foreach (var filePath in jsonFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                // Skip empty files
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    continue;
                }

                if (krsNumbers != null && krsNumbers.Length > 0 && !krsNumbers.Contains(fileName))
                {
                    continue;
                }

                KrsEntityDto? entity = null;
                try
                {
                    var jsonContent = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(jsonContent))
                    {
                        continue;
                    }

                    var doc = JsonNode.Parse(jsonContent);

                    var krsNumber = fileName;
                    var name = doc?["odpis"]?["dane"]?["dzial1"]?["danePodmiotu"]?["nazwa"]?.GetValue<string>() ?? "Brak nazwy";

                    // Extract address information
                    var address = ExtractAddress(doc);

                    // Try to find activity descriptions in various possible locations
                    var allDescriptions = new List<string>();

                    // Method 1: Look for celDzialaniaOrganizacji
                    var celDzialania = doc?["odpis"]?["dane"]?["dzial3"]?["celDzialaniaOrganizacji"]?["celDzialania"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(celDzialania))
                    {
                        allDescriptions.Add(celDzialania);
                    }

                    // Method 2: Look for przedmiotDzialalnosciOPP.nieodplatnyPkd[].opis
                    var pkdDescriptions = new List<string>();
                    var pkdNode = doc?["odpis"]?["dane"]?["dzial3"]?["przedmiotDzialalnosciOPP"]?["nieodplatnyPkd"];
                    if (pkdNode != null && pkdNode is JsonArray pkdArray)
                    {
                        foreach (var pkdItem in pkdArray)
                        {
                            var opis = pkdItem?["opis"]?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(opis))
                            {
                                pkdDescriptions.Add(opis);
                            }
                        }
                    }
                    allDescriptions.AddRange(pkdDescriptions);

                    // Method 3: Look for other possible activity description fields
                    var otherActivityFields = new[] { "przedmiotDzialalnosci", "działalność", "cel", "opis" };
                    foreach (var field in otherActivityFields)
                    {
                        var value = doc?["odpis"]?["dane"]?["dzial3"]?[field]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            allDescriptions.Add(value);
                        }
                    }

                    // Method 4: Look in dzial1 for additional information
                    var dzial1Info = doc?["odpis"]?["dane"]?["dzial1"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(dzial1Info))
                    {
                        // Extract any meaningful text from dzial1
                        var lines = dzial1Info.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(d => d.Trim())
                            .Where(d => !string.IsNullOrWhiteSpace(d) && d.Length > 10)
                            .Where(d => !d.Contains("\"") && !d.Contains("{") && !d.Contains("}"));
                        allDescriptions.AddRange(lines);
                    }

                    // If no descriptions found, skip this entity
                    if (!allDescriptions.Any())
                    {
                        Console.WriteLine($"No descriptions found for {krsNumber}, skipping");
                        continue;
                    }

                    // Filter by categories if specified
                    if (filterCategories != null && filterCategories.Any())
                    {
                        bool match = allDescriptions.Any(desc => filterCategories.Any(cat =>
                            desc.Contains(cat, StringComparison.OrdinalIgnoreCase)));

                        if (!match)
                        {
                            continue;
                        }
                    }

                    entity = new KrsEntityDto
                    {
                        KrsNumber = krsNumber,
                        Name = name,
                        ActivityDescriptions = allDescriptions.ToArray(),
                        Address = address
                    };

                    Console.WriteLine($"Found entity: {krsNumber} - {name} with {allDescriptions.Count} descriptions");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    continue;
                }

                if (entity != null)
                {
                    yield return entity;
                }
            }
        }

        public IEnumerable<KrsEntityDto> GetEntitiesByLocation(string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null)
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Console.WriteLine($"Data directory not found: {_dataDirectory}");
                yield break;
            }

            var jsonFiles = Directory.GetFiles(_dataDirectory, "*.json");
            Console.WriteLine($"Found {jsonFiles.Length} JSON files to process for location search");

            foreach (var filePath in jsonFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                // Skip empty files
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    continue;
                }

                KrsEntityDto? entity = null;
                try
                {
                    var jsonContent = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(jsonContent))
                    {
                        continue;
                    }

                    var doc = JsonNode.Parse(jsonContent);

                    var krsNumber = fileName;
                    var name = doc?["odpis"]?["dane"]?["dzial1"]?["danePodmiotu"]?["nazwa"]?.GetValue<string>() ?? "Brak nazwy";

                    // Extract address information
                    var address = ExtractAddress(doc);

                    // Check location filters
                    if (!MatchesLocationFilter(address, wojewodztwo, powiat, gmina, miejscowosc))
                    {
                        continue;
                    }

                    // Extract activity descriptions (same logic as GetEntities)
                    var allDescriptions = ExtractActivityDescriptions(doc);

                    // If no descriptions found, still include the entity for location search
                    if (!allDescriptions.Any())
                    {
                        allDescriptions.Add("Brak opisu działalności");
                    }

                    entity = new KrsEntityDto
                    {
                        KrsNumber = krsNumber,
                        Name = name,
                        ActivityDescriptions = allDescriptions.ToArray(),
                        Address = address
                    };

                    Console.WriteLine($"Found entity by location: {krsNumber} - {name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    continue;
                }

                if (entity != null)
                {
                    yield return entity;
                }
            }
        }

        public IEnumerable<string> GetNamesByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return Enumerable.Empty<string>();
            return GetEntities(null, new[] { category })
                .Select(e => e.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();
        }

        public IEnumerable<string> GetNamesByCategories(string[] categories)
        {
            if (categories == null || categories.Length == 0) return Enumerable.Empty<string>();
            return GetEntities(null, categories)
                .Select(e => e.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();
        }

        public IEnumerable<string> GetNamesByLocation(string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null)
        {
            return GetEntitiesByLocation(wojewodztwo, powiat, gmina, miejscowosc)
                .Select(e => e.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();
        }

        public IEnumerable<string> GetWojewodztwa()
        {
            return GetEntities()
                .Where(e => e.Address != null && !string.IsNullOrWhiteSpace(e.Address.Wojewodztwo))
                .Select(e => e.Address!.Wojewodztwo)
                .Distinct()
                .OrderBy(w => w);
        }

        public IEnumerable<string> GetPowiaty(string? wojewodztwo = null)
        {
            var entities = wojewodztwo != null
                ? GetEntitiesByLocation(wojewodztwo: wojewodztwo)
                : GetEntities();

            return entities
                .Where(e => e.Address != null && !string.IsNullOrWhiteSpace(e.Address.Powiat))
                .Select(e => e.Address!.Powiat)
                .Distinct()
                .OrderBy(p => p);
        }

        public IEnumerable<string> GetGminy(string? wojewodztwo = null, string? powiat = null)
        {
            var entities = GetEntitiesByLocation(wojewodztwo: wojewodztwo, powiat: powiat);

            return entities
                .Where(e => e.Address != null && !string.IsNullOrWhiteSpace(e.Address.Gmina))
                .Select(e => e.Address!.Gmina)
                .Distinct()
                .OrderBy(g => g);
        }

        public IEnumerable<string> GetMiejscowosci(string? wojewodztwo = null, string? powiat = null, string? gmina = null)
        {
            var entities = GetEntitiesByLocation(wojewodztwo: wojewodztwo, powiat: powiat, gmina: gmina);

            return entities
                .Where(e => e.Address != null && !string.IsNullOrWhiteSpace(e.Address.Miejscowosc))
                .Select(e => e.Address!.Miejscowosc)
                .Distinct()
                .OrderBy(m => m);
        }

        private AddressDto? ExtractAddress(JsonNode? doc)
        {
            try
            {
                var siedzibaIAdres = doc?["odpis"]?["dane"]?["dzial1"]?["siedzibaIAdres"];
                if (siedzibaIAdres == null) return null;

                var siedziba = siedzibaIAdres["siedziba"];
                var adres = siedzibaIAdres["adres"];

                if (siedziba == null || adres == null) return null;

                return new AddressDto
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

        private List<string> ExtractActivityDescriptions(JsonNode? doc)
        {
            var allDescriptions = new List<string>();

            // Method 1: Look for celDzialaniaOrganizacji
            var celDzialania = doc?["odpis"]?["dane"]?["dzial3"]?["celDzialaniaOrganizacji"]?["celDzialania"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(celDzialania))
            {
                allDescriptions.Add(celDzialania);
            }

            // Method 2: Look for przedmiotDzialalnosciOPP.nieodplatnyPkd[].opis
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

            // Method 3: Look for other possible activity description fields
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

        private bool MatchesLocationFilter(AddressDto? address, string? wojewodztwo, string? powiat, string? gmina, string? miejscowosc)
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