namespace ParasolBackEnd.Models
{
    public class OrganizacjaImportDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("krs")]
        public string Krs { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("nazwa")]
        public string Nazwa { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("adresy")]
        public List<AdresImportDto> Adresy { get; set; } = new List<AdresImportDto>();
        
        [System.Text.Json.Serialization.JsonPropertyName("koordynaty")]
        public List<KoordynatyImportDto> Koordynaty { get; set; } = new List<KoordynatyImportDto>();
        
        [System.Text.Json.Serialization.JsonPropertyName("celeStatusowe")]
        public List<string> CeleStatusowe { get; set; } = new List<string>();
    }

    public class AdresImportDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("numerKrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("ulica")]
        public string? Ulica { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("nrDomu")]
        public string? NrDomu { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("nrLokalu")]
        public string? NrLokalu { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("miejscowosc")]
        public string? Miejscowosc { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("kodPocztowy")]
        public string? KodPocztowy { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("poczta")]
        public string? Poczta { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("gmina")]
        public string? Gmina { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("powiat")]
        public string? Powiat { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("wojewodztwo")]
        public string? Wojewodztwo { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("kraj")]
        public string? Kraj { get; set; }
    }

    public class KoordynatyImportDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("numerKrs")]
        public string NumerKrs { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("latitude")]
        public double Latitude { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}
