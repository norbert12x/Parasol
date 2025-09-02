using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models.MatchMaker
{
    public class Post
    {
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public string OfferDescription { get; set; } = string.Empty;
        
        [Required]
        public string ContactInfo { get; set; } = string.Empty;
        
        [Required]
        public string Status { get; set; } = "active"; // active, inactive, closed
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ExpiresAt { get; set; }
        
        public int OrganizationId { get; set; }
        
        // Navigation properties
        public virtual Organization Organization { get; set; } = null!;
        public virtual ICollection<PostCategory> PostCategories { get; set; } = new List<PostCategory>();
        public virtual ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    }
}
