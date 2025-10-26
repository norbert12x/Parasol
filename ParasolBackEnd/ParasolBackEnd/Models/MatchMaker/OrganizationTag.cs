namespace ParasolBackEnd.Models.MatchMaker
{
    public class OrganizationTag
    {
        public int OrganizationId { get; set; }
        public int TagId { get; set; }
        
        // Navigation properties
        public virtual Organization Organization { get; set; } = null!;
        public virtual Tag Tag { get; set; } = null!;
    }
}

