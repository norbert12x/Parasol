using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.Services;
using System.Threading.Tasks;

namespace ParasolBackEnd.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrganizacjeController : ControllerBase
    {
        private readonly OrganizacjaService _organizacjaService;

        public OrganizacjeController(OrganizacjaService organizacjaService)
        {
            _organizacjaService = organizacjaService;
        }

        [HttpGet("{krs}")]
        public async Task<IActionResult> GetOrganizacja(string krs)
        {
            var org = await _organizacjaService.WczytajOrganizacjeAsync(krs);
            if (org == null)
                return NotFound();

            return Ok(org);
        }
    }
}
