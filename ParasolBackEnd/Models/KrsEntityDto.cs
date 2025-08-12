namespace ParasolBackEnd.Models;

public class KrsEntityDto
{
    public string KrsNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] ActivityDescriptions { get; set; } = System.Array.Empty<string>();
} 