using System.Threading.Tasks;
using ParasolBackEnd.Models;

namespace ParasolBackEnd.Services
{
    public interface IGeolocationService
    {
        Task<Koordynaty?> GetCoordinatesAsync(string address);
    }
}
