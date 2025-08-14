namespace ParasolBackEnd.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using ParasolBackEnd.Services;
    using System.Linq;
    using System.Threading.Tasks;

    [ApiController]
    [Route("[controller]")]
    public class OrganizacjeController : ControllerBase
    {
        private readonly OrganizacjaService _organizacjaService;
        private readonly KrsService _krsService;

        public OrganizacjeController(OrganizacjaService organizacjaService, KrsService krsService)
        {
            _organizacjaService = organizacjaService;
            _krsService = krsService;
        }

        // GET /Organizacje/{krs}
        [HttpGet("{krs}")]
        public async Task<IActionResult> GetOrganizacja(string krs)
        {
            var org = await _organizacjaService.WczytajOrganizacjeAsync(krs);
            if (org == null) return NotFound();
            return Ok(org);
        }

        // GET /Organizacje/kategoria/{category}
        // Zwraca listę organizacji (KRS, nazwa, adresy, geolokalizacja) powiązanych z kategorią
        [HttpGet("kategoria/{category}")]
        public async Task<IActionResult> GetByCategory(string category)
        {
            var entities = _krsService.GetEntities(null, new[] { category });

            var tasks = entities.Select(async e =>
            {
                var org = await _organizacjaService.WczytajOrganizacjeAsync(e.KrsNumber);
                if (org == null) return null;

                // Upewniamy się, że nazwa jest ustawiona
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
}
