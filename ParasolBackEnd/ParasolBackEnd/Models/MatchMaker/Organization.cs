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
        
        // Navigation properties
        public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    }
}
