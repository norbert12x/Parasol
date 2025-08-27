using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models
{
    public class Organizacja
    {
        [Key]
        public string NumerKRS { get; set; } = string.Empty;
        public string Nazwa { get; set; } = string.Empty;
        public virtual ICollection<Adres> Adresy { get; set; } = new List<Adres>();
        public virtual ICollection<Koordynaty> Koordynaty { get; set; } = new List<Koordynaty>();
        public virtual ICollection<OrganizacjaKategoria> OrganizacjaKategorie { get; set; } = new List<OrganizacjaKategoria>();
    }
}
