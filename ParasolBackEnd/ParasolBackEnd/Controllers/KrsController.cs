using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.Services;
using ParasolBackEnd.Models;

namespace ParasolBackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KrsController : ControllerBase
{
    private readonly IDatabaseService _databaseService;

    public KrsController(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// Returns the list of available categories.
    /// </summary>
    /// <returns>List of categories with ID and name.</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<Kategoria>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<Kategoria>>> GetCategories()
    {
        try
        {
            var result = await _databaseService.GetKategorieAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns organizations filtered by various criteria for map display.
    /// </summary>
    /// <param name="kategoria">Optional category filter.</param>
    /// <param name="wojewodztwo">Optional wojewodztwo filter.</param>
    /// <param name="powiat">Optional powiat filter.</param>
    /// <param name="gmina">Optional gmina filter.</param>
    /// <param name="miejscowosc">Optional miejscowosc filter.</param>
    /// <param name="krsNumber">Optional specific KRS number filter.</param>
    /// <returns>Collection of organizations with basic info, address and coordinates for map display.</returns>
    [HttpGet("map")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<object>>> GetOrganizationsForMap(
        [FromQuery] string? kategoria,
        [FromQuery] string? wojewodztwo,
        [FromQuery] string? powiat,
        [FromQuery] string? gmina,
        [FromQuery] string? miejscowosc,
        [FromQuery] string? krsNumber)
    {
        try
        {
            // Validate KRS number format if provided
            if (!string.IsNullOrWhiteSpace(krsNumber) && !IsValidKrsNumber(krsNumber))
            {
                return BadRequest($"Invalid KRS number format: {krsNumber}. KRS numbers should be 10-digit numbers.");
            }

            // Validate location parameters
            var validationError = ValidateLocationParameter("wojewodztwo", wojewodztwo, 100) ??
                                ValidateLocationParameter("powiat", powiat, 100) ??
                                ValidateLocationParameter("gmina", gmina, 100) ??
                                ValidateLocationParameter("miejscowosc", miejscowosc, 100) ??
                                ValidateLocationParameter("kategoria", kategoria, 200);

            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var organizacje = await _databaseService.GetOrganizationsForMapAsync(kategoria, wojewodztwo, powiat, gmina, miejscowosc, krsNumber);
            
            // Mapowanie na DTOs (tymczasowo inline)
            var result = organizacje.Select(org => new
            {
                KrsNumber = org.NumerKrs,
                Name = org.Nazwa,
                Address = org.Adresy.FirstOrDefault() != null ? new
                {
                    Wojewodztwo = org.Adresy.First().Wojewodztwo ?? "",
                    Powiat = org.Adresy.First().Powiat ?? "",
                    Gmina = org.Adresy.First().Gmina ?? "",
                    Miejscowosc = org.Adresy.First().Miejscowosc ?? "",
                    Ulica = org.Adresy.First().Ulica ?? "",
                    NrDomu = org.Adresy.First().NrDomu ?? "",
                    NrLokalu = org.Adresy.First().NrLokalu,
                    KodPocztowy = org.Adresy.First().KodPocztowy ?? "",
                    Poczta = org.Adresy.First().Poczta
                } : null,
                Latitude = org.Koordynaty.FirstOrDefault()?.Latitude,
                Longitude = org.Koordynaty.FirstOrDefault()?.Longitude
            });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates if the provided string is a valid KRS number format.
    /// </summary>
    /// <param name="krsNumber">The KRS number to validate.</param>
    /// <returns>True if the KRS number is valid, false otherwise.</returns>
    private static bool IsValidKrsNumber(string krsNumber)
    {
        if (string.IsNullOrWhiteSpace(krsNumber))
            return false;

        // KRS numbers should be 10-digit numbers
        return krsNumber.Length == 10 && krsNumber.All(char.IsDigit);
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
