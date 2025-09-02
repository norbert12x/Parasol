using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models.MatchMaker
{
    public class Category
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        // Navigation properties
        public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
        public virtual ICollection<PostCategory> PostCategories { get; set; } = new List<PostCategory>();
    }
}
