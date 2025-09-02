namespace ParasolBackEnd.Models.MatchMaker
{
    public class PostCategory
    {
        public int PostId { get; set; }
        public int CategoryId { get; set; }
        
        // Navigation properties
        public virtual Post Post { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}
