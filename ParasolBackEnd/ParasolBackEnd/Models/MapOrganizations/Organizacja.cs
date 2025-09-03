using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models.MapOrganizations
{
    public class Organizacja
    {
        [Key]
        [Column("numerkrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [Column("nazwa")]
        public string Nazwa { get; set; } = string.Empty;

        // Właściwość tymczasowa do importu (nie mapowana na bazę)
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> CeleStatusowe { get; set; } = new List<string>();

        // Właściwości nawigacyjne (bez relacji w DbContext)
        public ICollection<Adres> Adresy { get; set; } = new List<Adres>();
        public ICollection<Koordynaty> Koordynaty { get; set; } = new List<Koordynaty>();
        public ICollection<OrganizacjaKategoria> OrganizacjaKategorie { get; set; } = new List<OrganizacjaKategoria>();
    }
}
