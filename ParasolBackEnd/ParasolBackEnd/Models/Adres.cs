using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models
{
    public class Adres
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("numerkrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [Column("ulica")]
        public string? Ulica { get; set; }
        
        [Column("nrdomu")]
        public string? NrDomu { get; set; }
        
        [Column("nrlokalu")]
        public string? NrLokalu { get; set; }
        
        [Column("miejscowosc")]
        public string? Miejscowosc { get; set; }
        
        [Column("kodpocztowy")]
        public string? KodPocztowy { get; set; }
        
        [Column("poczta")]
        public string? Poczta { get; set; }
        
        [Column("gmina")]
        public string? Gmina { get; set; }
        
        [Column("powiat")]
        public string? Powiat { get; set; }
        
        [Column("wojewodztwo")]
        public string? Wojewodztwo { get; set; }
        
        [Column("kraj")]
        public string Kraj { get; set; } = string.Empty;

        // Właściwość nawigacyjna (bez relacji w DbContext)
        public virtual Organizacja Organizacja { get; set; } = null!;
    }
}
