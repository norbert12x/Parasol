using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParasolBackEnd.Services
{
    public class KrsEntityDto
    {
        public string KrsNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string[] ActivityDescriptions { get; set; } = Array.Empty<string>();
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
                        ActivityDescriptions = allDescriptions.ToArray()
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
    }
} 