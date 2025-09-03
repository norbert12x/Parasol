using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models.MapOrganizations
{
    public class OrganizacjaKategoria
    {
        [Column("numerkrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [Column("kategoriaid")]
        public int KategoriaId { get; set; }

        // Relacje nawigacyjne
        public Organizacja Organizacja { get; set; } = null!;
        public Kategoria Kategoria { get; set; } = null!;
    }
}
