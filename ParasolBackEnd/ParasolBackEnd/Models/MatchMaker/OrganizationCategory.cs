namespace ParasolBackEnd.Models.MatchMaker
{
    public class OrganizationCategory
    {
        public int OrganizationId { get; set; }
        public int CategoryId { get; set; }
        
        // Navigation properties
        public virtual Organization Organization { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}

