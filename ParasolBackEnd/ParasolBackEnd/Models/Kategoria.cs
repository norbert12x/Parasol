using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParasolBackEnd.Models
{
    public class Kategoria
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("nazwa")]
        public string Nazwa { get; set; } = string.Empty;
    }
}
