namespace ParasolBackEnd.DTOs
{
    /// DTO reprezentujący organizację z geolokalizacji.
    public class GeolocationEntityDto
    {
        public string KrsNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string[] ActivityDescriptions { get; set; } = Array.Empty<string>();
        public GeolocationAddressDto? Address { get; set; }
    }

    /// DTO reprezentujący adres organizacji.
    public class GeolocationAddressDto
    {
        public string Wojewodztwo { get; set; } = string.Empty;
        public string Powiat { get; set; } = string.Empty;
        public string Gmina { get; set; } = string.Empty;
        public string Miejscowosc { get; set; } = string.Empty;
        public string Ulica { get; set; } = string.Empty;
        public string NrDomu { get; set; } = string.Empty;
        public string? NrLokalu { get; set; }
        public string KodPocztowy { get; set; } = string.Empty;
        public string? Poczta { get; set; }
    }
}
