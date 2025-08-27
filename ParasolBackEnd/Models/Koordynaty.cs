using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models
{
    public class Koordynaty
    {
        [Key]
        public int Id { get; set; }
        public string NumerKRS { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Właściwość nawigacyjna
        public virtual Organizacja Organizacja { get; set; } = null!;
    }
}
