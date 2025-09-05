namespace ParasolBackEnd.Models
{
    public class ImportResult
    {
        public int ImportedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> DeletedFiles { get; set; } = new List<string>();
        public bool Success => Errors.Count == 0;
    }
}
