namespace ParasolBackEnd.Models
{
    public class Organizacja
    {
        public string Nazwa { get; set; }
        public List<Adres> Adresy { get; set; }
        public List<Koordynaty> Geolokalizacja { get; set; } = new();

    }
}
