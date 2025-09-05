using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.Models.MapOrganizations;
using ParasolBackEnd.Services;
using ParasolBackEnd.Models;

namespace ParasolBackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(IDatabaseService databaseService, ILogger<DatabaseController> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

        [HttpGet("test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var isConnected = await _databaseService.TestConnectionAsync();
                
                if (isConnected)
                {
                    return Ok(new { message = "Database connection successful", status = "connected" });
                }
                else
                {
                    return BadRequest(new { message = "Database connection failed", status = "disconnected" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing database connection");
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet("organizacje")]
        [ProducesResponseType(typeof(IEnumerable<Organizacja>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Organizacja>>> GetOrganizacje()
        {
            try
            {
                var organizacje = await _databaseService.GetOrganizacjeAsync();
                return Ok(organizacje);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizacje");
                return StatusCode(500, new { message = "Error retrieving organizacje", error = ex.Message });
            }
        }

        [HttpGet("organizacje/{numerKrs}")]
        [ProducesResponseType(typeof(Organizacja), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Organizacja>> GetOrganizacjaByKrs(string numerKrs)
        {
            try
            {
                var organizacja = await _databaseService.GetOrganizacjaByKrsAsync(numerKrs);
                
                if (organizacja == null)
                {
                    return NotFound(new { message = $"Organizacja with KRS {numerKrs} not found" });
                }

                return Ok(organizacja);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizacja by KRS: {NumerKrs}", numerKrs);
                return StatusCode(500, new { message = "Error retrieving organizacja", error = ex.Message });
            }
        }

        [HttpPost("organizacje")]
        [ProducesResponseType(typeof(Organizacja), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Organizacja>> CreateOrganizacja([FromBody] Organizacja organizacja)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _databaseService.SaveOrganizacjaAsync(organizacja);
                
                if (success)
                {
                    return CreatedAtAction(nameof(GetOrganizacjaByKrs), 
                        new { numerKrs = organizacja.NumerKrs }, organizacja);
                }
                else
                {
                    return BadRequest(new { message = "Failed to create organizacja" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organizacja");
                return StatusCode(500, new { message = "Error creating organizacja", error = ex.Message });
            }
        }

        [HttpPut("organizacje/{numerKrs}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrganizacja(string numerKrs, [FromBody] Organizacja organizacja)
        {
            try
            {
                if (numerKrs != organizacja.NumerKrs)
                {
                    return BadRequest(new { message = "KRS mismatch" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _databaseService.UpdateOrganizacjaAsync(organizacja);
                
                if (success)
                {
                    return Ok(new { message = "Organizacja updated successfully" });
                }
                else
                {
                    return NotFound(new { message = $"Organizacja with KRS {numerKrs} not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organizacja: {NumerKrs}", numerKrs);
                return StatusCode(500, new { message = "Error updating organizacja", error = ex.Message });
            }
        }

        [HttpDelete("organizacje/{numerKrs}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOrganizacja(string numerKrs)
        {
            try
            {
                var success = await _databaseService.DeleteOrganizacjaAsync(numerKrs);
                
                if (success)
                {
                    return Ok(new { message = "Organizacja deleted successfully" });
                }
                else
                {
                    return NotFound(new { message = $"Organizacja with KRS {numerKrs} not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organizacja: {NumerKrs}", numerKrs);
                return StatusCode(500, new { message = "Error deleting organizacja", error = ex.Message });
            }
        }

        [HttpPost("import-z-geolokalizacji")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportFromGeolokalizacja([FromQuery] string? wojewodztwo)
        {
            try
            {
                _logger.LogInformation("Starting import from geolokalizacja for wojewodztwo: {Wojewodztwo}", wojewodztwo ?? "all");
                
                var result = await _databaseService.ImportFromGeolokalizacjaAsync(wojewodztwo);
                
                return Ok(new { 
                    message = "Import completed successfully", 
                    importedCount = result.ImportedCount,
                    deletedFilesCount = result.DeletedFiles.Count,
                    deletedFiles = result.DeletedFiles,
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during import from geolokalizacja");
                return StatusCode(500, new { message = "Error during import", error = ex.Message });
            }
        }
    }
}
