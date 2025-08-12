using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.Models;
using ParasolBackEnd.Services;

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
    [ProducesResponseType(typeof(IEnumerable<KrsEntityDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<KrsEntityDto>> GetEntities([FromQuery] string[]? krsNumbers, [FromQuery] string[]? categories)
    {
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
        if (!string.IsNullOrWhiteSpace(category))
        {
            return Ok(_service.GetNamesByCategory(category));
        }

        if (categories != null && categories.Length > 0)
        {
            return Ok(_service.GetNamesByCategories(categories));
        }

        return BadRequest("Provide 'category' or 'categories' query parameter(s).");
    }
} 