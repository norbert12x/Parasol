using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.Services;

namespace ParasolBackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizacjeGeolokalizacjaController : ControllerBase
{
    private readonly OrganizacjaService _organizacjaService;
    private readonly KrsService _krsService;

    public OrganizacjeGeolokalizacjaController(OrganizacjaService organizacjaService, KrsService krsService)
    {
        _organizacjaService = organizacjaService;
        _krsService = krsService;
    }

    /// <summary>
    /// Zwraca listę organizacji z geolokalizacją, opcjonalnie filtrowanych po kategorii i/lub lokalizacji.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrganizacje(
        [FromQuery] string[]? categories,
        [FromQuery] string? wojewodztwo,
        [FromQuery] string? powiat,
        [FromQuery] string? gmina,
        [FromQuery] string? miejscowosc)
    {
        // Pobierz organizacje z KRS według kategorii i lokalizacji
        var krsEntities = _krsService.GetEntitiesByLocation(
            wojewodztwo, powiat, gmina, miejscowosc);

        if (categories != null && categories.Length > 0)
        {
            krsEntities = krsEntities.Where(e =>
                e.ActivityDescriptions.Any(desc =>
                    categories.Any(cat => desc.Contains(cat, StringComparison.OrdinalIgnoreCase))));
        }

        // Pobierz pełne dane z OrganizacjaService, żeby mieć geolokalizację
        var tasks = krsEntities.Select(async e =>
        {
            var org = await _organizacjaService.WczytajOrganizacjeAsync(e.KrsNumber);
            if (org == null) return null;

            // Jeśli nazwa w OrganizacjaService jest pusta, użyj nazwy z KRS
            if (string.IsNullOrWhiteSpace(org.Nazwa))
                org.Nazwa = e.Name;

            return new
            {
                Krs = e.KrsNumber,
                Nazwa = org.Nazwa,
                Adresy = org.Adresy,
                Geolokalizacja = org.Geolokalizacja
            };
        });

        var result = (await Task.WhenAll(tasks))
            .Where(x => x != null)
            .ToList();

        return Ok(result);
    }
}
