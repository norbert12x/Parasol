using System.ComponentModel.DataAnnotations;

namespace ParasolBackEnd.Models.MatchMaker
{
    public class Organization
    {
        public int Id { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Required]
        public string OrganizationName { get; set; } = string.Empty;
        
        public string? KrsNumber { get; set; }
        
        // Navigation properties
        public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    }
}
