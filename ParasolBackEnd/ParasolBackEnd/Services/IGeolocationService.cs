using System.Threading.Tasks;
using ParasolBackEnd.Models;
using ParasolBackEnd.Services;

namespace ParasolBackEnd.Services
{
    public interface IGeolocationService
    {
        Task<Koordynaty?> GetCoordinatesAsync(string address);
        Task<List<GeolocationEntityDto>> GetEntitiesByLocationAsync(string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null);
        IEnumerable<GeolocationEntityDto> GetEntitiesByLocation(string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null);
    }
}
