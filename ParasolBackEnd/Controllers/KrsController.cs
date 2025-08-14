using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.Services;
using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KrsController : ControllerBase
{
    private readonly KrsService _service;

    public KrsController(KrsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns the list of supported category phrases.
    /// </summary>
    /// <returns>Array of category phrases.</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetCategories()
    {
        return Ok(_service.GetCategories());
    }

    /// <summary>
    /// Returns KRS entities optionally filtered by KRS numbers and/or categories.
    /// </summary>
    /// <param name="krsNumbers">Optional list of KRS numbers to include.</param>
    /// <param name="categories">Optional list of categories to match against activity descriptions.</param>
    /// <returns>Collection of KRS entities.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<object>> GetEntities([FromQuery] string[]? krsNumbers, [FromQuery] string[]? categories)
    {
        // Validate KRS numbers format
        if (krsNumbers != null && krsNumbers.Length > 0)
        {
            var invalidKrsNumbers = krsNumbers.Where(krs => !IsValidKrsNumber(krs)).ToArray();
            if (invalidKrsNumbers.Length > 0)
            {
                return BadRequest($"Invalid KRS number format(s): {string.Join(", ", invalidKrsNumbers)}. KRS numbers should be 10-digit numbers.");
            }
        }

        // Validate categories
        if (categories != null && categories.Length > 0)
        {
            var invalidCategories = categories.Where(cat => string.IsNullOrWhiteSpace(cat)).ToArray();
            if (invalidCategories.Length > 0)
            {
                return BadRequest("Category parameters cannot be null or empty.");
            }

            if (categories.Length > 50)
            {
                return BadRequest("Maximum 50 categories can be specified at once.");
            }
        }

        var result = _service.GetEntities(krsNumbers, categories);
        return Ok(result);
    }

    /// <summary>
    /// Returns only organization names for a given category or set of categories.
    /// </summary>
    /// <param name="category">Single category to match.</param>
    /// <param name="categories">Multiple categories to match. Use repeated query parameters.</param>
    /// <returns>Distinct list of organization names.</returns>
    [HttpGet("names")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<string>> GetNames([FromQuery] string? category, [FromQuery] string[]? categories)
    {
        // Validate category parameter
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (category.Length > 200)
            {
                return BadRequest("Category parameter cannot exceed 200 characters.");
            }
            return Ok(_service.GetNamesByCategory(category));
        }

        // Validate categories array
        if (categories != null && categories.Length > 0)
        {
            var invalidCategories = categories.Where(cat => string.IsNullOrWhiteSpace(cat)).ToArray();
            if (invalidCategories.Length > 0)
            {
                return BadRequest("Category parameters cannot be null or empty.");
            }

            if (categories.Length > 50)
            {
                return BadRequest("Maximum 50 categories can be specified at once.");
            }

            var tooLongCategories = categories.Where(cat => cat.Length > 200).ToArray();
            if (tooLongCategories.Length > 0)
            {
                return BadRequest($"Category parameters cannot exceed 200 characters: {string.Join(", ", tooLongCategories)}");
            }

            return Ok(_service.GetNamesByCategories(categories));
        }

        return BadRequest("Either 'category' or 'categories' parameter must be provided.");
    }

    /// <summary>
    /// Returns KRS entities filtered by location criteria.
    /// </summary>
    /// <param name="wojewodztwo">Optional województwo filter.</param>
    /// <param name="powiat">Optional powiat filter.</param>
    /// <param name="gmina">Optional gmina filter.</param>
    /// <param name="miejscowosc">Optional miejscowość filter.</param>
    /// <returns>Collection of KRS entities matching location criteria.</returns>
    [HttpGet("location")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<object>> GetEntitiesByLocation(
        [FromQuery] string? wojewodztwo,
        [FromQuery] string? powiat,
        [FromQuery] string? gmina,
        [FromQuery] string? miejscowosc)
    {
        // Validate location parameters
        if (!string.IsNullOrWhiteSpace(wojewodztwo) && wojewodztwo.Length > 100)
        {
            return BadRequest("Województwo parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(powiat) && powiat.Length > 100)
        {
            return BadRequest("Powiat parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(gmina) && gmina.Length > 100)
        {
            return BadRequest("Gmina parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(miejscowosc) && miejscowosc.Length > 100)
        {
            return BadRequest("Miejscowość parameter cannot exceed 100 characters.");
        }

        var result = _service.GetEntitiesByLocation(wojewodztwo, powiat, gmina, miejscowosc);
        return Ok(result);
    }

    /// <summary>
    /// Returns only organization names filtered by both category and location criteria.
    /// </summary>
    /// <param name="category">Single category to match.</param>
    /// <param name="categories">Multiple categories to match. Use repeated query parameters.</param>
    /// <param name="wojewodztwo">Optional województwo filter.</param>
    /// <param name="powiat">Optional powiat filter.</param>
    /// <param name="gmina">Optional gmina filter.</param>
    /// <param name="miejscowosc">Optional miejscowość filter.</param>
    /// <returns>Distinct list of organization names matching both category and location criteria.</returns>
    [HttpGet("names/location")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<string>> GetNamesByLocation(
        [FromQuery] string? category,
        [FromQuery] string[]? categories,
        [FromQuery] string? wojewodztwo,
        [FromQuery] string? powiat,
        [FromQuery] string? gmina,
        [FromQuery] string? miejscowosc)
    {
        // Validate category parameters
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (category.Length > 200)
            {
                return BadRequest("Category parameter cannot exceed 200 characters.");
            }
        }

        if (categories != null && categories.Length > 0)
        {
            var invalidCategories = categories.Where(cat => string.IsNullOrWhiteSpace(cat)).ToArray();
            if (invalidCategories.Length > 0)
            {
                return BadRequest("Category parameters cannot be null or empty.");
            }

            if (categories.Length > 50)
            {
                return BadRequest("Maximum 50 categories can be specified at once.");
            }

            var tooLongCategories = categories.Where(cat => cat.Length > 200).ToArray();
            if (tooLongCategories.Length > 0)
            {
                return BadRequest($"Category parameters cannot exceed 200 characters: {string.Join(", ", tooLongCategories)}");
            }
        }

        // Validate location parameters
        if (!string.IsNullOrWhiteSpace(wojewodztwo) && wojewodztwo.Length > 100)
        {
            return BadRequest("Województwo parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(powiat) && powiat.Length > 100)
        {
            return BadRequest("Powiat parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(gmina) && gmina.Length > 100)
        {
            return BadRequest("Gmina parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(miejscowosc) && miejscowosc.Length > 100)
        {
            return BadRequest("Miejscowość parameter cannot exceed 100 characters.");
        }

        // Check if any location parameters are provided
        bool hasLocationFilter = !string.IsNullOrWhiteSpace(wojewodztwo) || 
                                !string.IsNullOrWhiteSpace(powiat) || 
                                !string.IsNullOrWhiteSpace(gmina) || 
                                !string.IsNullOrWhiteSpace(miejscowosc);

        // If no location filter, use the same logic as regular names endpoint
        if (!hasLocationFilter)
        {
            if (!string.IsNullOrWhiteSpace(category))
            {
                return Ok(_service.GetNamesByCategory(category));
            }

            if (categories != null && categories.Length > 0)
            {
                return Ok(_service.GetNamesByCategories(categories));
            }

            return BadRequest("Either 'category' or 'categories' parameter must be provided when no location filter is specified.");
        }

        // Get entities filtered by location
        var locationEntities = _service.GetEntitiesByLocation(wojewodztwo, powiat, gmina, miejscowosc);
        
        // Apply category filtering
        if (!string.IsNullOrWhiteSpace(category))
        {
            var filteredEntities = locationEntities.Where(e => 
                e.ActivityDescriptions.Any(desc => 
                    desc.Contains(category, StringComparison.OrdinalIgnoreCase)));
            return Ok(filteredEntities.Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct());
        }

        if (categories != null && categories.Length > 0)
        {
            var filteredEntities = locationEntities.Where(e => 
                e.ActivityDescriptions.Any(desc => 
                    categories.Any(cat => desc.Contains(cat, StringComparison.OrdinalIgnoreCase))));
            return Ok(filteredEntities.Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct());
        }

        // If no category parameters provided, return all names from location filter
        return Ok(locationEntities.Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct());
    }

    /// <summary>
    /// Returns all available województwa.
    /// </summary>
    /// <returns>List of województwa names.</returns>
    [HttpGet("wojewodztwa")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetWojewodztwa()
    {
        var result = _service.GetWojewodztwa();
        return Ok(result);
    }

    /// <summary>
    /// Returns available powiaty, optionally filtered by województwo.
    /// </summary>
    /// <param name="wojewodztwo">Optional województwo filter.</param>
    /// <returns>List of powiat names.</returns>
    [HttpGet("powiaty")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<string>> GetPowiaty([FromQuery] string? wojewodztwo)
    {
        if (!string.IsNullOrWhiteSpace(wojewodztwo) && wojewodztwo.Length > 100)
        {
            return BadRequest("Województwo parameter cannot exceed 100 characters.");
        }

        var result = _service.GetPowiaty(wojewodztwo);
        return Ok(result);
    }

    /// <summary>
    /// Returns available gminy, optionally filtered by województwo and/or powiat.
    /// </summary>
    /// <param name="wojewodztwo">Optional województwo filter.</param>
    /// <param name="powiat">Optional powiat filter.</param>
    /// <returns>List of gmina names.</returns>
    [HttpGet("gminy")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<string>> GetGminy([FromQuery] string? wojewodztwo, [FromQuery] string? powiat)
    {
        if (!string.IsNullOrWhiteSpace(wojewodztwo) && wojewodztwo.Length > 100)
        {
            return BadRequest("Województwo parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(powiat) && powiat.Length > 100)
        {
            return BadRequest("Powiat parameter cannot exceed 100 characters.");
        }

        var result = _service.GetGminy(wojewodztwo, powiat);
        return Ok(result);
    }

    /// <summary>
    /// Returns available miejscowości, optionally filtered by województwo, powiat, and/or gmina.
    /// </summary>
    /// <param name="wojewodztwo">Optional województwo filter.</param>
    /// <param name="powiat">Optional powiat filter.</param>
    /// <param name="gmina">Optional gmina filter.</param>
    /// <returns>List of miejscowość names.</returns>
    [HttpGet("miejscowosci")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IEnumerable<string>> GetMiejscowosci([FromQuery] string? wojewodztwo, [FromQuery] string? powiat, [FromQuery] string? gmina)
    {
        if (!string.IsNullOrWhiteSpace(wojewodztwo) && wojewodztwo.Length > 100)
        {
            return BadRequest("Województwo parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(powiat) && powiat.Length > 100)
        {
            return BadRequest("Powiat parameter cannot exceed 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(gmina) && gmina.Length > 100)
        {
            return BadRequest("Gmina parameter cannot exceed 100 characters.");
        }

        var result = _service.GetMiejscowosci(wojewodztwo, powiat, gmina);
        return Ok(result);
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
}
