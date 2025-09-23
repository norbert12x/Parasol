#pragma warning disable CS8619, CS8602
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ParasolBackEnd.Services;

namespace ParasolBackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizacjeController : ControllerBase
{
    private readonly OrganizacjaService _organizacjaService;
    private readonly IGeolocationService _geolocationService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrganizacjeController> _logger;

    public OrganizacjeController(OrganizacjaService organizacjaService, IGeolocationService geolocationService, HttpClient httpClient, ILogger<OrganizacjeController> logger)
    {
        _organizacjaService = organizacjaService;
        _geolocationService = geolocationService;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Zwraca listę organizacji z geolokalizacją, opcjonalnie filtrowanych po kategorii i/lub lokalizacji.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<object>>> GetOrganizacje(
        [FromQuery] string[]? categories,
        [FromQuery] string? wojewodztwo,
        [FromQuery] string? powiat,
        [FromQuery] string? gmina,
        [FromQuery] string? miejscowosc,
        [FromQuery] int? limit)
    {
        try
        {
            // Validate parameters
            var validationError = ValidateLocationParameter("wojewodztwo", wojewodztwo, 100) ??
                                ValidateLocationParameter("powiat", powiat, 100) ??
                                ValidateLocationParameter("gmina", gmina, 100) ??
                                ValidateLocationParameter("miejscowosc", miejscowosc, 100);

            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            _logger.LogInformation("Getting organizations with filters: categories={Categories}, wojewodztwo={Wojewodztwo}, powiat={Powiat}, gmina={Gmina}, miejscowosc={Miejscowosc}",
                categories?.Length ?? 0, wojewodztwo ?? "ALL", powiat ?? "ALL", gmina ?? "ALL", miejscowosc ?? "ALL");

            // Pobierz organizacje z geolokalizacji według lokalizacji - TERAZ ASYNC!
            var krsEntities = await _geolocationService.GetEntitiesByLocationAsync(
                wojewodztwo, powiat, gmina, miejscowosc);

            // Wymuś maksymalny limit 45 rekordów na żądanie (domyślnie 45)
            var effectiveLimit = Math.Min(limit ?? 45, 45);
            krsEntities = krsEntities.Take(effectiveLimit).ToList();

            if (categories != null && categories.Length > 0)
            {
                krsEntities = krsEntities.Where(e =>
                    e.ActivityDescriptions.Any(desc =>
                        categories.Any(cat => desc.Contains(cat, StringComparison.OrdinalIgnoreCase)))).ToList();
            }

            // Pobierz pełne dane z OrganizacjaService, żeby mieć geolokalizację
            var tasks = krsEntities.Select(async e =>
            {
                try
                {
                    var org = await _organizacjaService.LoadOrganizationAsync(e.KrsNumber);
                    if (org == null) return null;

                    // Jeśli nazwa w OrganizacjaService jest pusta, użyj nazwy z KRS
                    if (string.IsNullOrWhiteSpace(org.Nazwa))
                        org.Nazwa = e.Name;

                    return new
                    {
                        Krs = e.KrsNumber,
                        Nazwa = org.Nazwa,
                        Adresy = org.Adresy,
                        Koordynaty = org.Koordynaty,
                        CeleStatusowe = e.ActivityDescriptions
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load organization details for KRS {Krs}", e.KrsNumber);
                    return null;
                }
            });

            var result = (await Task.WhenAll(tasks))
                .Where(x => x != null)
                .ToList();

            _logger.LogInformation("Returning {Count} organizations", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizations");
            return StatusCode(500, $"Wystąpił błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Pobiera dane organizacji z API KRS i zapisuje do folderu dane.
    /// </summary>
    [HttpPost("krs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> GetKrsData([FromBody] List<string> krsNumbers, [FromQuery] string rejestr = "S")
    {
        try
        {
            // Walidacja danych wejściowych
            if (krsNumbers == null || !krsNumbers.Any())
            {
                return BadRequest(new { message = "Lista numerów KRS nie może być pusta" });
            }

            if (krsNumbers.Count > 10000)
            {
                return BadRequest(new { message = "Maksymalnie 10000 numerów KRS na raz" });
            }

            if (rejestr != "S" && rejestr != "P")
            {
                return BadRequest(new { message = "Parametr rejestr musi być 'S' (stowarzyszenia) lub 'P' (przedsiębiorcy)" });
            }

            // Walidacja formatu numerów KRS
            var invalidKrsNumbers = krsNumbers.Where(krs => 
                string.IsNullOrWhiteSpace(krs) || 
                !krs.All(char.IsDigit) || 
                krs.Length != 10).ToList();

            if (invalidKrsNumbers.Any())
            {
                return BadRequest(new { 
                    message = "Nieprawidłowy format numerów KRS", 
                    invalidNumbers = invalidKrsNumbers 
                });
            }

            _logger.LogInformation("Pobieranie danych KRS dla {Count} organizacji, rejestr: {Rejestr}", krsNumbers.Count, rejestr);

            var dataFolder = Path.Combine("..", "dane");
            Directory.CreateDirectory(dataFolder);

            var tasks = krsNumbers.Select(async krsNumber =>
            {
                try
                {
                    var url = $"https://api-krs.ms.gov.pl/api/krs/OdpisAktualny/{krsNumber}?rejestr={rejestr}&format=json";
                    var response = await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var fileName = $"{krsNumber}.json";
                        var filePath = Path.Combine(dataFolder, fileName);
                        
                        await System.IO.File.WriteAllTextAsync(filePath, content);
                        
                        return new
                        {
                            KrsNumber = krsNumber,
                            Success = true,
                            StatusCode = (int)response.StatusCode,
                            FilePath = filePath,
                            ErrorMessage = (string?)null
                        };
                    }
                    else
                    {
                        return new
                        {
                            KrsNumber = krsNumber,
                            Success = false,
                            StatusCode = (int)response.StatusCode,
                            FilePath = (string?)null,
                            ErrorMessage = $"Błąd {response.StatusCode}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas pobierania danych dla KRS: {KrsNumber}", krsNumber);
                    return new
                    {
                        KrsNumber = krsNumber,
                        Success = false,
                        StatusCode = 500,
                        FilePath = (string?)null,
                        ErrorMessage = ex.Message
                    };
                }
            });

            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            _logger.LogInformation("Zakończono pobieranie danych KRS: {Success}/{Total} pomyślnie", successCount, krsNumbers.Count);

            return Ok(new
            {
                TotalRequested = krsNumbers.Count,
                SuccessfullyProcessed = successCount,
                Failed = failureCount,
                DataFolder = dataFolder,
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania danych KRS");
            return StatusCode(500, $"Wystąpił błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Usuwa wszystkie puste pliki JSON z folderu dane.
    /// </summary>
    [HttpDelete("krs/empty")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> DeleteEmptyJsonFiles()
    {
        try
        {
            var dataFolder = Path.Combine("..", "dane");
            
            if (!Directory.Exists(dataFolder))
            {
                return Ok(new
                {
                    Message = "Folder dane nie istnieje",
                    DeletedFiles = 0,
                    TotalFiles = 0
                });
            }

            var jsonFiles = Directory.GetFiles(dataFolder, "*.json");
            var deletedFiles = new List<string>();
            var totalFiles = jsonFiles.Length;

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var content = await System.IO.File.ReadAllTextAsync(filePath);
                    
                    // Sprawdź czy plik jest pusty lub zawiera tylko białe znaki
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        System.IO.File.Delete(filePath);
                        deletedFiles.Add(Path.GetFileName(filePath));
                        _logger.LogInformation("Usunięto pusty plik: {FileName}", Path.GetFileName(filePath));
                    }
                    else
                    {
                        // Sprawdź czy to pusty JSON (tylko {} lub [])
                        var trimmedContent = content.Trim();
                        if (trimmedContent == "{}" || trimmedContent == "[]" || trimmedContent == "null")
                        {
                            System.IO.File.Delete(filePath);
                            deletedFiles.Add(Path.GetFileName(filePath));
                            _logger.LogInformation("Usunięto pusty JSON: {FileName}", Path.GetFileName(filePath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas sprawdzania pliku: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Usunięto {Count} pustych plików JSON z {Total} plików", deletedFiles.Count, totalFiles);

            return Ok(new
            {
                Message = $"Usunięto {deletedFiles.Count} pustych plików JSON",
                DeletedFiles = deletedFiles.Count,
                TotalFiles = totalFiles,
                DeletedFileNames = deletedFiles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania pustych plików JSON");
            return StatusCode(500, $"Wystąpił błąd: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates location parameter length.
    /// </summary>
    /// <param name="paramName">Parameter name for error message.</param>
    /// <param name="value">Value to validate.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <returns>Error message if validation fails, null otherwise.</returns>
    private static string? ValidateLocationParameter(string paramName, string? value, int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
        {
            return $"{paramName} parameter cannot exceed {maxLength} characters.";
        }
        return null;
    }
}
