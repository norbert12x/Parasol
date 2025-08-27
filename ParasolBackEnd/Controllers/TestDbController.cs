using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParasolBackEnd.Data;

namespace ParasolBackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestDbController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TestDbController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("ping")]
        public async Task<IActionResult> PingDb()
        {
            try
            {
                // Spróbuj wykonać prosty SELECT
                var first = await _context.Kategorie.FirstOrDefaultAsync();
                return Ok(new { message = "Połączenie działa ✅", sample = first });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Błąd połączenia ❌", error = ex.Message });
            }
        }
    }
}
