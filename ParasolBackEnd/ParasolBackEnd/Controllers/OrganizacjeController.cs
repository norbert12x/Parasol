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
    private readonly ILogger<OrganizacjeController> _logger;

    public OrganizacjeController(OrganizacjaService organizacjaService, IGeolocationService geolocationService, ILogger<OrganizacjeController> logger)
    {
        _organizacjaService = organizacjaService;
        _geolocationService = geolocationService;
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
        [FromQuery] string? miejscowosc)
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

            if (categories != null && categories.Length > 0)
            {
                krsEntities = krsEntities.Where(e =>
                    e.ActivityDescriptions.Any(desc =>
                        categories.Any(cat => desc.Contains(cat, StringComparison.OrdinalIgnoreCase)))).ToList();
            }

            // Pobierz pełne dane z OrganizacjaService, żeby mieć geolokalizację
            var tasks = krsEntities.Select(async e =>
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
