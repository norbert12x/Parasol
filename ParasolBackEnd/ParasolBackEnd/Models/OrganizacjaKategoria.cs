using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models
{
    public class OrganizacjaKategoria
    {
        [Column("numerkrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [Column("kategoriaid")]
        public int KategoriaId { get; set; }

        // Relacje nawigacyjne
        public virtual Organizacja Organizacja { get; set; } = null!;
        public virtual Kategoria Kategoria { get; set; } = null!;
    }
}
