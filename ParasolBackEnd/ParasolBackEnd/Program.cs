using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;


namespace ParasolBackEnd
{
    public class Program
    {
        static async Task Main()
        {
            // Lista przyk³adowych numerów KRS
            string[] krsNumbers = new string[]
            {
                "0000281817",
                "0000509762",
                "0000258081",
                "0000334696",
                "0000175882",
                "0000579141",
                "0001027770",
                "0000128380",
                "0000865131",
                "0000321173",
                "0000466506",
                "0000096794",
                "0000960152",
                "0000360415",
                "0000497368",
                "0000257074",
                "0000305861",
                "0000492486",
                "0000160110",
                "0000588668",
                "0000696141",
                "0000981190",
                "0000234727",
                "0000131935",
                "0000010843",
                "0001109495",
                "0000451014",
                "0000301921",
                "0000102399",
                "0001062347",
                "0000338053",
                "0000159405",
                "0000251833",
                "0000377987",
                "0000763858",
                "0000390358",
                "0000733609",
                "0000151051",
                "0000147699",
                "0000155746",
                "0000189678",
                "0001147607",
                "0000673918",
                "0000681447",
                "0000162641",
                "0001172496",
                "0000179585",
                "0000367427",
                "0000234234",
                "0000077832",
                "0000441465",
                "0000254981",
                "0000251069",
                "0000242593",
                "0000432290",
                "0000447790",
                "0000509555",
                "0000431452",
                "0000312488",
                "0000242223"
            };


            // Folder docelowy
            string outputDir = @"D:\projekty\ParasolBackEnd\dane";
            Directory.CreateDirectory(outputDir);

            using HttpClient client = new HttpClient();

            foreach (var krs in krsNumbers)
            {
                try
                {
                    string url = $"https://api-krs.ms.gov.pl/api/krs/OdpisAktualny/{krs}?rejestr=S&format=json";
                    Console.WriteLine($"Pobieranie danych KRS {krs}...");

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        string filePath = Path.Combine(outputDir, $"{krs}.json");
                        await File.WriteAllTextAsync(filePath, json);
                        Console.WriteLine($"Zapisano plik: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine($"B³¹d {response.StatusCode} dla KRS {krs}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"B³¹d przy pobieraniu {krs}: {ex.Message}");
                }
            }

            Console.WriteLine("Pobieranie zakoñczone.");
        }

    }
}
