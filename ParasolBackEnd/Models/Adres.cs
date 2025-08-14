namespace ParasolBackEnd.Models
{
    public class Adres
    {
        public string Ulica { get; set; }
        public string NrDomu { get; set; }
        public string NrLokalu { get; set; }
        public string Miejscowosc { get; set; }
        public string KodPocztowy { get; set; }
        public string Poczta { get; set; }
        public string Gmina { get; set; }
        public string Powiat { get; set; }
        public string Wojewodztwo { get; set; }
        public string Kraj { get; set; }

        public string PelnyAdres()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Ulica)) parts.Add(Ulica);
            if (!string.IsNullOrEmpty(NrDomu)) parts.Add(NrDomu);
            if (!string.IsNullOrEmpty(NrLokalu)) parts.Add("lok." + NrLokalu);
            if (!string.IsNullOrEmpty(Miejscowosc)) parts.Add(Miejscowosc);
            if (!string.IsNullOrEmpty(KodPocztowy)) parts.Add(KodPocztowy);
            if (!string.IsNullOrEmpty(Poczta)) parts.Add(Poczta);
            if (!string.IsNullOrEmpty(Gmina)) parts.Add(Gmina);
            if (!string.IsNullOrEmpty(Powiat)) parts.Add(Powiat);
            if (!string.IsNullOrEmpty(Wojewodztwo)) parts.Add(Wojewodztwo);
            if (!string.IsNullOrEmpty(Kraj)) parts.Add(Kraj);
            return string.Join(", ", parts);
        }
    }
}
