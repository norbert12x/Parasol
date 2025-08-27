using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models
{
    public class Adres
    {
        [Key]
        public int Id { get; set; }
        public string NumerKRS { get; set; } = string.Empty;
        public string Ulica { get; set; } = string.Empty;
        public string NrDomu { get; set; } = string.Empty;
        public string? NrLokalu { get; set; }
        public string Miejscowosc { get; set; } = string.Empty;
        public string KodPocztowy { get; set; } = string.Empty;
        public string Poczta { get; set; } = string.Empty;
        public string Gmina { get; set; } = string.Empty;
        public string Powiat { get; set; } = string.Empty;
        public string Wojewodztwo { get; set; } = string.Empty;
        public string Kraj { get; set; } = string.Empty;

        // Właściwość nawigacyjna
        public virtual Organizacja Organizacja { get; set; } = null!;
    }
}
