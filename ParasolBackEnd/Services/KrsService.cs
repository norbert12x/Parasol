using System.Text.Json.Nodes;
using ParasolBackEnd.Models;
using Microsoft.Extensions.Options;

namespace ParasolBackEnd.Services;

public class KrsService
{
    private readonly string _dataDir;
    private readonly string[] _categoryPhrases = new[]
    {
        "pomoc społeczna",
        "działalność charytatywna",
        "ochrona zdrowia",
        "edukacja",
        "kultura i sztuka",
        "turystyka",
        "sport",
        "ochrona środowiska",
        "działalność religijna",
        "rozrywka i rekreacja",
        "działalność gospodarcza",
        "działalność naukowa",
        "rozwój regionalny",
        "pomoc humanitarna",
        "rehabilitacja zawodowa i społeczna",
        "aktywizacja zawodowa",
        "działalność wydawnicza",
        "ochrona praw konsumentów",
        "pomoc ofiarom przestępstw",
        "działalność wspomagająca przedsiębiorczość",
        "usługi socjalne",
        "promocja zdrowia",
        "edukacja ekologiczna",
        "wspieranie rodziny i systemu pieczy zastępczej"
    };

    public KrsService(IOptions<AppSettings> options)
    {
        _dataDir = options.Value.DataDirectory;
    }

    public string[] GetCategories() => _categoryPhrases;

    public IEnumerable<KrsEntityDto> GetEntities(string[]? filterKrsNumbers, string[]? filterCategories)
    {
        if (string.IsNullOrWhiteSpace(_dataDir) || !Directory.Exists(_dataDir))
        {
            yield break;
        }

        var files = Directory.GetFiles(_dataDir, "*.json");

        var filteredFiles = (filterKrsNumbers != null && filterKrsNumbers.Any())
            ? files.Where(f => filterKrsNumbers.Contains(Path.GetFileNameWithoutExtension(f)))
            : files.AsEnumerable();

        foreach (var file in filteredFiles)
        {
            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            JsonNode? doc;
            try
            {
                doc = JsonNode.Parse(json);
            }
            catch
            {
                continue;
            }

            if (doc == null) continue;

            string krsNumber = Path.GetFileNameWithoutExtension(file);
            
            // Navigate to the correct path for name: odpis.dane.dzial1.danePodmiotu.nazwa
            string name = doc["odpis"]?["dane"]?["dzial1"]?["danePodmiotu"]?["nazwa"]?.GetValue<string>() ?? "Brak nazwy";

            // Navigate to the correct path for activity descriptions: odpis.dane.dzial3.celDzialaniaOrganizacji.celDzialania
            var activityDescNode = doc["odpis"]?["dane"]?["dzial3"]?["celDzialaniaOrganizacji"]?["celDzialania"];
            string activityDescription = activityDescNode?.GetValue<string>() ?? string.Empty;

            // Split the activity description by newlines and filter out empty lines
            var descriptions = activityDescription
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToArray();

            if (filterCategories != null && filterCategories.Any())
            {
                bool match = descriptions.Any(desc => filterCategories.Any(cat =>
                    desc.Contains(cat, StringComparison.OrdinalIgnoreCase)));

                if (!match) continue;
            }

            yield return new KrsEntityDto
            {
                KrsNumber = krsNumber,
                Name = name,
                ActivityDescriptions = descriptions
            };
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