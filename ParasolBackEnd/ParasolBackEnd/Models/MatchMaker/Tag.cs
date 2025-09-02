using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models.MatchMaker
{
    public class Tag
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public int CategoryId { get; set; }
        
        // Navigation properties
        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    }
}
