using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models.MatchMaker
{
    public class Organization
    {
        public int Id { get; set; }
        
        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string OrganizationName { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string? KrsNumber { get; set; }
        
        // Role
        public string Role { get; set; } = "user";
        
        // Profile fields
        public string? AboutText { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Phone { get; set; }
        public string? ContactEmail { get; set; }
        
        // Navigation properties
        public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
        public virtual ICollection<OrganizationCategory> OrganizationCategories { get; set; } = new List<OrganizationCategory>();
        public virtual ICollection<OrganizationTag> OrganizationTags { get; set; } = new List<OrganizationTag>();
    }
}
