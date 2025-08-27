using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models
{
    public class Koordynaty
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("numerkrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [Column("latitude")]
        public double Latitude { get; set; }
        
        [Column("longitude")]
        public double Longitude { get; set; }

        // Właściwość nawigacyjna (bez relacji w DbContext)
        public virtual Organizacja Organizacja { get; set; } = null!;
    }
}
