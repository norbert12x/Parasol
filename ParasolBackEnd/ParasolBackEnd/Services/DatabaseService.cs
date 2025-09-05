using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ParasolBackEnd.Data;
using ParasolBackEnd.Models;
using ParasolBackEnd.Models.MapOrganizations;
using ParasolBackEnd.DTOs;
using Npgsql;
using System.Text.Json;

namespace ParasolBackEnd.Services
{
    public interface IDatabaseService
    {
        Task<bool> TestConnectionAsync();
        Task<List<Organizacja>> GetOrganizacjeAsync();
        Task<Organizacja?> GetOrganizacjaByKrsAsync(string numerKrs);
        Task<List<Kategoria>> GetKategorieAsync();
        Task<bool> SaveOrganizacjaAsync(Organizacja organizacja);
        Task<bool> UpdateOrganizacjaAsync(Organizacja organizacja);
        Task<bool> DeleteOrganizacjaAsync(string numerKrs);
        Task<ImportResult> ImportFromGeolokalizacjaAsync(string? wojewodztwo = null);
        Task<List<Organizacja>> GetOrganizationsForMapAsync(string? kategoria = null, string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null, string? krsNumber = null);
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseService> _logger;
        private readonly string _connectionString;

        public DatabaseService(AppDbContext context, ILogger<DatabaseService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") + ";Multiplexing=false;Pooling=false";
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                _logger.LogInformation("Database connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                return false;
            }
        }

        public async Task<List<Organizacja>> GetOrganizacjeAsync()
        {
            try
            {
                return await _context.Organizacje
                    .Include(o => o.Adresy)
                    .Include(o => o.Koordynaty)
                    .Include(o => o.OrganizacjaKategorie)
                        .ThenInclude(ok => ok.Kategoria)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizacje");
                return new List<Organizacja>();
            }
        }

        public async Task<Organizacja?> GetOrganizacjaByKrsAsync(string numerKrs)
        {
            try
            {
                return await _context.Organizacje
                    .Include(o => o.Adresy)
                    .Include(o => o.Koordynaty)
                    .Include(o => o.OrganizacjaKategorie)
                        .ThenInclude(ok => ok.Kategoria)
                    .FirstOrDefaultAsync(o => o.NumerKrs == numerKrs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizacja by KRS: {NumerKrs}", numerKrs);
                return null;
            }
        }

        public async Task<List<Kategoria>> GetKategorieAsync()
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                const string query = "SELECT id, nazwa FROM kategoria ORDER BY id";
                await using var command = new NpgsqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();
                
                var kategorie = new List<Kategoria>();
                while (await reader.ReadAsync())
                {
                    var kategoria = new Kategoria
                    {
                        Id = reader.GetInt32(0),
                        Nazwa = reader.GetString(1)
                    };
                    kategorie.Add(kategoria);
                }
                
                _logger.LogDebug("Retrieved {Count} kategorie from database", kategorie.Count);
                return kategorie;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting kategorie using direct Npgsql");
                return new List<Kategoria>();
            }
        }

        public async Task<bool> SaveOrganizacjaAsync(Organizacja organizacja)
        {
            try
            {
                var existing = await _context.Organizacje
                    .FirstOrDefaultAsync(o => o.NumerKrs == organizacja.NumerKrs);

                if (existing != null)
                {
                    _logger.LogWarning("Organizacja with KRS {NumerKrs} already exists", organizacja.NumerKrs);
                    return false;
                }

                _context.Organizacje.Add(organizacja);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Organizacja saved successfully: {NumerKrs}", organizacja.NumerKrs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving organizacja: {NumerKrs}", organizacja.NumerKrs);
                return false;
            }
        }

        public async Task<bool> UpdateOrganizacjaAsync(Organizacja organizacja)
        {
            try
            {
                var existing = await _context.Organizacje
                    .Include(o => o.Adresy)
                    .Include(o => o.Koordynaty)
                    .Include(o => o.OrganizacjaKategorie)
                    .FirstOrDefaultAsync(o => o.NumerKrs == organizacja.NumerKrs);

                if (existing == null)
                {
                    _logger.LogWarning("Organizacja with KRS {NumerKrs} not found for update", organizacja.NumerKrs);
                    return false;
                }

                // Aktualizuj podstawowe dane organizacji
                existing.Nazwa = organizacja.Nazwa;

                // Usuń stare adresy i dodaj nowe
                _context.Adresy.RemoveRange(existing.Adresy);
                existing.Adresy.Clear();
                foreach (var adres in organizacja.Adresy)
                {
                    adres.NumerKrs = organizacja.NumerKrs;
                    existing.Adresy.Add(adres);
                }

                // Usuń stare koordynaty i dodaj nowe
                _context.Koordynaty.RemoveRange(existing.Koordynaty);
                existing.Koordynaty.Clear();
                foreach (var koordynaty in organizacja.Koordynaty)
                {
                    koordynaty.NumerKrs = organizacja.NumerKrs;
                    existing.Koordynaty.Add(koordynaty);
                }

                // Usuń stare kategorie i dodaj nowe
                _context.OrganizacjaKategorie.RemoveRange(existing.OrganizacjaKategorie);
                existing.OrganizacjaKategorie.Clear();
                foreach (var kategoria in organizacja.OrganizacjaKategorie)
                {
                    kategoria.NumerKrs = organizacja.NumerKrs;
                    existing.OrganizacjaKategorie.Add(kategoria);
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Organizacja updated successfully: {NumerKrs} with {AdresCount} adresy, {KoordynatyCount} koordynaty, {KategorieCount} kategorie", 
                    organizacja.NumerKrs, organizacja.Adresy.Count, organizacja.Koordynaty.Count, organizacja.OrganizacjaKategorie.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organizacja: {NumerKrs}", organizacja.NumerKrs);
                return false;
            }
        }

        public async Task<bool> DeleteOrganizacjaAsync(string numerKrs)
        {
            try
            {
                var organizacja = await _context.Organizacje
                    .FirstOrDefaultAsync(o => o.NumerKrs == numerKrs);

                if (organizacja == null)
                {
                    _logger.LogWarning("Organizacja with KRS {NumerKrs} not found for deletion", numerKrs);
                    return false;
                }

                _context.Organizacje.Remove(organizacja);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Organizacja deleted successfully: {NumerKrs}", numerKrs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organizacja: {NumerKrs}", numerKrs);
                return false;
            }
        }

        public async Task<ImportResult> ImportFromGeolokalizacjaAsync(string? wojewodztwo = null)
        {
            _logger.LogInformation("Starting import from geolokalizacja");
            
            try
            {
                var organizacje = await GetOrganizacjeFromGeolokalizacjaAsync(wojewodztwo);
                _logger.LogInformation("Found {Count} organizations to import", organizacje.Count);

                // Ograniczenie do maksymalnie 4000 organizacji
                if (organizacje.Count > 4000)
                {
                    _logger.LogWarning("Too many organizations ({Count}), limiting to 4000", organizacje.Count);
                    organizacje = organizacje.Take(4000).ToList();
                }

                var importedCount = 0;
                var errors = new List<string>();
                var deletedFiles = new List<string>();

                foreach (var organizacja in organizacje)
                {
                    try
                    {
                        // Sprawdź czy organizacja już istnieje
                        if (await OrganizacjaExistsAsync(organizacja.NumerKrs))
                        {
                            _logger.LogDebug("Organizacja {Krs} already exists, will UPDATE", organizacja.NumerKrs);
                        }
                        else
                        {
                            _logger.LogDebug("Organizacja {Krs} is new, will INSERT", organizacja.NumerKrs);
                        }

                        // Użyj bezpośrednio Npgsql zamiast Entity Framework
                        await SaveOrganizacjaWithNpgsqlAsync(organizacja);
                        
                        // Usuń plik JSON po zaimportowaniu
                        var jsonFilePath = Path.Combine("..", "dane", $"{organizacja.NumerKrs}.json");
                        if (System.IO.File.Exists(jsonFilePath))
                        {
                            try
                            {
                                System.IO.File.Delete(jsonFilePath);
                                deletedFiles.Add(organizacja.NumerKrs);
                                _logger.LogDebug("Deleted JSON file: {FilePath}", jsonFilePath);
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogWarning(deleteEx, "Failed to delete JSON file: {FilePath}", jsonFilePath);
                            }
                        }
                        
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error importing organizacja {organizacja.NumerKrs}: {ex.Message}";
                        _logger.LogError(ex, errorMessage);
                        errors.Add(errorMessage);
                    }
                }

                _logger.LogInformation("Import completed. Imported: {ImportedCount}, Errors: {ErrorCount}, Deleted files: {DeletedCount}", 
                    importedCount, errors.Count, deletedFiles.Count);
                
                return new ImportResult 
                { 
                    ImportedCount = importedCount, 
                    Errors = errors,
                    DeletedFiles = deletedFiles
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during import process");
                return new ImportResult { ImportedCount = 0, Errors = new List<string> { ex.Message } };
            }
        }

        public async Task<List<Organizacja>> GetOrganizationsForMapAsync(string? kategoria = null, string? wojewodztwo = null, string? powiat = null, string? gmina = null, string? miejscowosc = null, string? krsNumber = null)
        {
            _logger.LogDebug("Getting organizations with filters: kategoria={Kategoria}, wojewodztwo={Wojewodztwo}, powiat={Powiat}, gmina={Gmina}, miejscowosc={Miejscowosc}, krsNumber={KrsNumber}", 
                kategoria ?? "ALL", wojewodztwo ?? "ALL", powiat ?? "ALL", gmina ?? "ALL", miejscowosc ?? "ALL", krsNumber ?? "ALL");
            
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                // Buduj dynamiczne zapytanie SQL z opcjonalnymi filtrami
                var whereConditions = new List<string>();
                var parameters = new List<NpgsqlParameter>();
                
                // Filtrowanie po kategorii
                if (!string.IsNullOrWhiteSpace(kategoria))
                {
                    whereConditions.Add("cat.nazwa ILIKE @kategoria");
                    parameters.Add(new NpgsqlParameter("@kategoria", $"%{kategoria}%"));
                }
                
                // Filtrowanie po województwie
                if (!string.IsNullOrWhiteSpace(wojewodztwo))
                {
                    whereConditions.Add("a.wojewodztwo ILIKE @wojewodztwo");
                    parameters.Add(new NpgsqlParameter("@wojewodztwo", $"%{wojewodztwo}%"));
                }
                
                // Filtrowanie po powiecie
                if (!string.IsNullOrWhiteSpace(powiat))
                {
                    whereConditions.Add("a.powiat ILIKE @powiat");
                    parameters.Add(new NpgsqlParameter("@powiat", $"%{powiat}%"));
                }
                
                // Filtrowanie po gminie
                if (!string.IsNullOrWhiteSpace(gmina))
                {
                    whereConditions.Add("a.gmina ILIKE @gmina");
                    parameters.Add(new NpgsqlParameter("@gmina", $"%{gmina}%"));
                }
                
                // Filtrowanie po miejscowości
                if (!string.IsNullOrWhiteSpace(miejscowosc))
                {
                    whereConditions.Add("a.miejscowosc ILIKE @miejscowosc");
                    parameters.Add(new NpgsqlParameter("@miejscowosc", $"%{miejscowosc}%"));
                }
                
                // Filtrowanie po numerze KRS
                if (!string.IsNullOrWhiteSpace(krsNumber))
                {
                    whereConditions.Add("o.numerkrs ILIKE @krsNumber");
                    parameters.Add(new NpgsqlParameter("@krsNumber", $"%{krsNumber}%"));
                }
                
                // ZOPTYMALIZOWANE ZAPYTANIE: Jeden SQL zamiast 3 osobnych!
                string optimizedQuery;
                if (whereConditions.Count > 0)
                {
                    // Z filtrami - wszystkie dane w jednym zapytaniu
                    optimizedQuery = @"
                        SELECT DISTINCT 
                            o.numerkrs, o.nazwa,
                            a.ulica, a.nrdomu, a.nrlokalu, a.miejscowosc, a.kodpocztowy, a.poczta, a.gmina, a.powiat, a.wojewodztwo, a.kraj,
                            k.latitude, k.longitude
                        FROM organizacja o
                        INNER JOIN organizacjakategoria ok ON o.numerkrs = ok.numerkrs
                        INNER JOIN kategoria cat ON ok.kategoriaid = cat.id
                        INNER JOIN adres a ON o.numerkrs = a.numerkrs
                        INNER JOIN koordynaty k ON o.numerkrs = k.numerkrs
                        WHERE " + string.Join(" AND ", whereConditions);
                }
                else
                {
                    // Bez filtrów - wszystkie dane w jednym zapytaniu
                    optimizedQuery = @"
                        SELECT DISTINCT 
                            o.numerkrs, o.nazwa,
                            a.ulica, a.nrdomu, a.nrlokalu, a.miejscowosc, a.kodpocztowy, a.poczta, a.gmina, a.powiat, a.wojewodztwo, a.kraj,
                            k.latitude, k.longitude
                        FROM organizacja o
                        INNER JOIN organizacjakategoria ok ON o.numerkrs = ok.numerkrs
                        INNER JOIN kategoria cat ON ok.kategoriaid = cat.id
                        INNER JOIN adres a ON o.numerkrs = a.numerkrs
                        INNER JOIN koordynaty k ON o.numerkrs = k.numerkrs";
                }
                
                var command = new NpgsqlCommand(optimizedQuery, connection);
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }
                
                _logger.LogDebug("Executing OPTIMIZED query with {FilterCount} filters", whereConditions.Count);
                
                await using var reader = await command.ExecuteReaderAsync();
                
                var organizacje = new List<Organizacja>();
                var organizacjeDict = new Dictionary<string, Organizacja>();
                
                while (await reader.ReadAsync())
                {
                    var numerKrs = reader.GetString(0);
                    
                    // Sprawdź czy organizacja już istnieje w słowniku
                    if (!organizacjeDict.TryGetValue(numerKrs, out var organizacja))
                    {
                        // Nowa organizacja
                        organizacja = new Organizacja
                        {
                            NumerKrs = numerKrs,
                            Nazwa = reader.GetString(1),
                            Adresy = new List<Adres>(),
                            Koordynaty = new List<Koordynaty>()
                        };
                        organizacje.Add(organizacja);
                        organizacjeDict[numerKrs] = organizacja;
                    }
                    
                    // Dodaj adres (jeśli nie jest duplikatem)
                    var adres = new Adres
                    {
                        Ulica = reader.IsDBNull(2) ? null : reader.GetString(2),
                        NrDomu = reader.IsDBNull(3) ? null : reader.GetString(3),
                        NrLokalu = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Miejscowosc = reader.IsDBNull(5) ? null : reader.GetString(5),
                        KodPocztowy = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Poczta = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Gmina = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Powiat = reader.IsDBNull(9) ? null : reader.GetString(9),
                        Wojewodztwo = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Kraj = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                        NumerKrs = numerKrs
                    };
                    
                    // Sprawdź czy adres już istnieje (unikaj duplikatów)
                    if (!organizacja.Adresy.Any(a => 
                        a.Ulica == adres.Ulica && 
                        a.Miejscowosc == adres.Miejscowosc && 
                        a.Gmina == adres.Gmina))
                    {
                        organizacja.Adresy.Add(adres);
                    }
                    
                    // Dodaj koordynaty (jeśli nie są duplikatem)
                    var koordynaty = new Koordynaty
                    {
                        Latitude = reader.GetDouble(12),
                        Longitude = reader.GetDouble(13),
                        NumerKrs = numerKrs
                    };
                    
                    // Sprawdź czy koordynaty już istnieją (unikaj duplikatów)
                    if (!organizacja.Koordynaty.Any(k => 
                        Math.Abs(k.Latitude - koordynaty.Latitude) < 0.000001 && 
                        Math.Abs(k.Longitude - koordynaty.Longitude) < 0.000001))
                    {
                        organizacja.Koordynaty.Add(koordynaty);
                    }
                }
                
                _logger.LogInformation("Successfully loaded {Count} organizations with OPTIMIZED query using {FilterCount} filters", organizacje.Count, whereConditions.Count);
                return organizacje;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizations with optimized query: {ErrorDetails}", ex.ToString());
                return new List<Organizacja>();
            }
        }

        private async Task<List<Organizacja>> GetOrganizacjeFromGeolokalizacjaAsync(string? wojewodztwo = null)
        {
            try
            {
                var url = "https://localhost:7170/api/Organizacje";
                if (!string.IsNullOrEmpty(wojewodztwo))
                {
                    url += $"?wojewodztwo={Uri.EscapeDataString(wojewodztwo)}";
                }

                _logger.LogDebug("Fetching data from: {Url}", url);

                using var client = new HttpClient();
                var response = await client.GetStringAsync(url);
                
                if (string.IsNullOrEmpty(response))
                {
                    _logger.LogWarning("Empty response from geolokalizacja endpoint");
                    return new List<Organizacja>();
                }

                var organizacjeDto = JsonSerializer.Deserialize<List<OrganizacjaImportDto>>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (organizacjeDto == null)
                {
                    _logger.LogWarning("Failed to deserialize response from geolokalizacja endpoint");
                    return new List<Organizacja>();
                }

                _logger.LogDebug("Deserialized {Count} organizacje from geolokalizacja endpoint", organizacjeDto.Count);

                var organizacje = new List<Organizacja>();
                foreach (var dto in organizacjeDto)
                {
                    var organizacja = new Organizacja
                    {
                        NumerKrs = dto.Krs,
                        Nazwa = dto.Nazwa,
                        CeleStatusowe = dto.CeleStatusowe,
                        Adresy = dto.Adresy.Select(a => new Adres
                        {
                            NumerKrs = a.NumerKrs,
                            Ulica = a.Ulica,
                            NrDomu = a.NrDomu,
                            NrLokalu = a.NrLokalu,
                            Miejscowosc = a.Miejscowosc,
                            KodPocztowy = a.KodPocztowy,
                            Poczta = a.Poczta,
                            Gmina = a.Gmina,
                            Powiat = a.Powiat,
                            Wojewodztwo = a.Wojewodztwo,
                            Kraj = a.Kraj ?? string.Empty
                        }).ToList(),
                        Koordynaty = dto.Koordynaty.Select(k => new Koordynaty
                        {
                            NumerKrs = k.NumerKrs,
                            Latitude = k.Latitude,
                            Longitude = k.Longitude
                        }).ToList()
                    };

                    organizacje.Add(organizacja);
                }

                _logger.LogDebug("Mapped {Count} organizacje with addresses and coordinates", organizacje.Count);
                return organizacje;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching organizacje from geolokalizacja endpoint");
                throw;
            }
        }

        private async Task<List<Kategoria>> AssignCategoriesAsync(List<string> celeStatusowe)
        {
            try
            {
                _logger.LogDebug("Starting category assignment for organizacja with {Count} CeleStatusowe", celeStatusowe.Count);
                
                // Pobierz wszystkie kategorie z bazy
                var kategorie = await GetKategorieAsync();
                var assignedCategories = new List<Kategoria>();

                // Słowa kluczowe dla każdej kategorii
                var categoryKeywords = new Dictionary<int, List<string>>
                {
                    { 1, new List<string> { "pomoc", "społeczna", "socjalna" } },
                    { 2, new List<string> { "rodzina", "dzieci", "dziecko", "rodzin" } },
                    { 3, new List<string> { "trudna", "sytuacja", "życiowa", "życie" } },
                    { 4, new List<string> { "charytatywna", "charytatywne", "dobroczynność" } },
                    { 5, new List<string> { "tradycja", "kultura", "narodowa", "naród" } },
                    { 6, new List<string> { "mniejszość", "narodowa", "etniczna", "etnicz" } },
                    { 7, new List<string> { "zdrowie", "medyczna", "opieka", "lekarz", "szpital" } },
                    { 8, new List<string> { "niepełnosprawn", "niepełnosprawność", "inwalida" } },
                    { 9, new List<string> { "zatrudnienie", "praca", "zawodowa", "aktywizacja" } },
                    { 10, new List<string> { "kobiet", "mężczyzn", "równe", "prawa" } },
                    { 11, new List<string> { "gospodarczy", "przedsiębiorczość", "biznes", "ekonomia" } },
                    { 12, new List<string> { "wspólnota", "lokalna", "lokalny", "społeczność" } },
                    { 13, new List<string> { "edukacja", "nauka", "szkoła", "uczelnia", "uniwersytet" } },
                    { 14, new List<string> { "wypoczynek", "dzieci", "młodzież", "wakacje", "kolonie" } },
                    { 15, new List<string> { "kultura", "sztuka", "artystyczna", "teatr", "muzeum" } },
                    { 16, new List<string> { "sport", "fizyczna", "wychowanie", "fizyczne", "aktywność" } },
                    { 17, new List<string> { "ekologia", "ochrona", "zwierząt", "środowisko", "przyroda" } },
                    { 18, new List<string> { "bezpieczeństwo", "porządek", "publiczny", "policja" } },
                    { 19, new List<string> { "obronność", "państwa", "wojsko", "armia" } },
                    { 20, new List<string> { "wolności", "prawa", "człowieka", "obywatel" } },
                    { 21, new List<string> { "ratownictwo", "ochrona", "ludności", "straż", "pożarna" } },
                    { 22, new List<string> { "katastrof", "wojen", "ofiar", "pomoc", "humanitarna" } },
                    { 23, new List<string> { "konsument", "prawa", "konsumenta", "ochrona" } },
                    { 24, new List<string> { "europejska", "międzynarodowa", "współpraca", "integracja" } },
                    { 25, new List<string> { "wolontariat", "wolontariusz", "dobrowolna" } },
                    { 26, new List<string> { "organizacja", "pozarządowa", "ngo", "fundacja" } },
                    { 27, new List<string> { "sport", "wychowanie", "fizyczne", "aktywność" } }
                };

                // Sprawdź każde słowo kluczowe
                foreach (var kvp in categoryKeywords)
                {
                    var kategoriaId = kvp.Key;
                    var keywords = kvp.Value;

                    foreach (var keyword in keywords)
                    {
                        if (celeStatusowe.Any(cele => 
                            cele.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                        {
                            var kategoria = kategorie.FirstOrDefault(k => k.Id == kategoriaId);
                            if (kategoria != null && !assignedCategories.Any(ac => ac.Id == kategoriaId))
                            {
                                assignedCategories.Add(kategoria);
                                break; // Znaleziono kategorię, przejdź do następnej
                            }
                        }
                    }
                }

                // Jeśli nie przypisano żadnej kategorii, dodaj "Inne działania społeczne" (ID: 28)
                if (!assignedCategories.Any())
                {
                    var kategoriaInne = kategorie.FirstOrDefault(k => k.Id == 28);
                    if (kategoriaInne != null)
                    {
                        assignedCategories.Add(kategoriaInne);
                    }
                }
                
                _logger.LogDebug("Assigned {Count} categories to organizacja", assignedCategories.Count);
                
                return assignedCategories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning categories to organizacja with CeleStatusowe: {CeleStatusowe}", 
                    string.Join(", ", celeStatusowe));
                throw; // Przekaż błąd dalej
            }
        }

        private async Task<bool> OrganizacjaExistsAsync(string numerKrs)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            string query = "SELECT COUNT(*) FROM organizacja WHERE numerkrs = @numerKrs";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@numerKrs", numerKrs);
            
            var count = await command.ExecuteScalarAsync();
            return Convert.ToInt32(count) > 0;
        }

        private async Task SaveOrganizacjaWithNpgsqlAsync(Organizacja organizacja)
        {
            _logger.LogDebug("Starting SaveOrganizacjaWithNpgsqlAsync for organizacja {NumerKrs}", organizacja.NumerKrs);
            
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            await using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                // Sprawdź czy organizacja istnieje
                bool organizacjaExists = await OrganizacjaExistsAsync(organizacja.NumerKrs);
                
                if (organizacjaExists)
                {
                    // 1. UPDATE istniejącej organizacji
                    _logger.LogDebug("Step 1: UPDATING existing organizacja {NumerKrs}", organizacja.NumerKrs);
                    string updateOrgQuery = "UPDATE organizacja SET nazwa = @nazwa WHERE numerkrs = @numerKrs";
                    await using var orgCommand = new NpgsqlCommand(updateOrgQuery, connection, transaction);
                    orgCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    orgCommand.Parameters.AddWithValue("@nazwa", organizacja.Nazwa);
                    await orgCommand.ExecuteNonQueryAsync();
                    
                    // Usuń stare adresy i koordynaty przed wstawieniem nowych
                    _logger.LogDebug("Deleting old data for organizacja {NumerKrs}", organizacja.NumerKrs);
                    string deleteAdresQuery = "DELETE FROM adres WHERE numerkrs = @numerKrs";
                    await using var deleteAdresCommand = new NpgsqlCommand(deleteAdresQuery, connection, transaction);
                    deleteAdresCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    await deleteAdresCommand.ExecuteNonQueryAsync();
                    
                    string deleteKoordynatyQuery = "DELETE FROM koordynaty WHERE numerkrs = @numerKrs";
                    await using var deleteKoordynatyCommand = new NpgsqlCommand(deleteKoordynatyQuery, connection, transaction);
                    deleteKoordynatyCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    await deleteKoordynatyCommand.ExecuteNonQueryAsync();
                    
                    string deleteKategorieQuery = "DELETE FROM organizacjakategoria WHERE numerkrs = @numerKrs";
                    await using var deleteKategorieCommand = new NpgsqlCommand(deleteKategorieQuery, connection, transaction);
                    deleteKategorieCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    await deleteKategorieCommand.ExecuteNonQueryAsync();
                }
                else
                {
                    // 1. INSERT nowej organizacji
                    _logger.LogDebug("Step 1: INSERTING new organizacja {NumerKrs}", organizacja.NumerKrs);
                    string insertOrgQuery = "INSERT INTO organizacja (numerkrs, nazwa) VALUES (@numerKrs, @nazwa)";
                    await using var orgCommand = new NpgsqlCommand(insertOrgQuery, connection, transaction);
                    orgCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    orgCommand.Parameters.AddWithValue("@nazwa", organizacja.Nazwa);
                    await orgCommand.ExecuteNonQueryAsync();
                }

                // 2. Zapisz adresy organizacji (po wstawieniu/aktualizacji głównej tabeli)
                _logger.LogDebug("Step 2: Inserting {Count} adresy for organizacja {NumerKrs}", organizacja.Adresy.Count, organizacja.NumerKrs);
                foreach (var adres in organizacja.Adresy)
                {
                    // Upewnij się, że adres ma poprawny numerkrs
                    adres.NumerKrs = organizacja.NumerKrs;
                    
                    string insertAdresQuery = @"INSERT INTO adres (numerkrs, ulica, nrdomu, nrlokalu, miejscowosc, kodpocztowy, poczta, gmina, powiat, wojewodztwo, kraj) 
                                               VALUES (@numerKrs, @ulica, @nrdomu, @nrlokalu, @miejscowosc, @kodpocztowy, @poczta, @gmina, @powiat, @wojewodztwo, @kraj)";
                    
                    await using var adresCommand = new NpgsqlCommand(insertAdresQuery, connection, transaction);
                    adresCommand.Parameters.AddWithValue("@numerKrs", adres.NumerKrs);
                    adresCommand.Parameters.AddWithValue("@ulica", adres.Ulica ?? "");
                    adresCommand.Parameters.AddWithValue("@nrdomu", adres.NrDomu ?? "");
                    adresCommand.Parameters.AddWithValue("@nrlokalu", adres.NrLokalu ?? "");
                    adresCommand.Parameters.AddWithValue("@miejscowosc", adres.Miejscowosc ?? "");
                    adresCommand.Parameters.AddWithValue("@kodpocztowy", adres.KodPocztowy ?? "");
                    adresCommand.Parameters.AddWithValue("@poczta", adres.Poczta ?? "");
                    adresCommand.Parameters.AddWithValue("@gmina", adres.Gmina ?? "");
                    adresCommand.Parameters.AddWithValue("@powiat", adres.Powiat ?? "");
                    adresCommand.Parameters.AddWithValue("@wojewodztwo", adres.Wojewodztwo ?? "");
                    adresCommand.Parameters.AddWithValue("@kraj", adres.Kraj ?? "");
                    
                    await adresCommand.ExecuteNonQueryAsync();
                }

                // 3. Zapisz koordynaty geograficzne organizacji (po wstawieniu/aktualizacji głównej tabeli)
                _logger.LogDebug("Step 3: Inserting {Count} koordynaty for organizacja {NumerKrs}", organizacja.Koordynaty.Count, organizacja.NumerKrs);
                foreach (var koordynaty in organizacja.Koordynaty)
                {
                    // Upewnij się, że koordynaty mają poprawny numerkrs
                    koordynaty.NumerKrs = organizacja.NumerKrs;
                    
                    string insertKoordynatyQuery = "INSERT INTO koordynaty (numerkrs, latitude, longitude) VALUES (@numerKrs, @latitude, @longitude)";
                    
                    await using var koordynatyCommand = new NpgsqlCommand(insertKoordynatyQuery, connection, transaction);
                    koordynatyCommand.Parameters.AddWithValue("@numerKrs", koordynaty.NumerKrs);
                    koordynatyCommand.Parameters.AddWithValue("@latitude", koordynaty.Latitude);
                    koordynatyCommand.Parameters.AddWithValue("@longitude", koordynaty.Longitude);
                    
                    await koordynatyCommand.ExecuteNonQueryAsync();
                }

                // 4. Przypisz kategorie na podstawie celów statutowych (po wstawieniu/aktualizacji głównej tabeli)
                if (organizacja.CeleStatusowe?.Any() == true)
                {
                    _logger.LogDebug("Step 4: Assigning categories for organizacja {NumerKrs} with {Count} CeleStatusowe", 
                        organizacja.NumerKrs, organizacja.CeleStatusowe.Count);
                    
                    var kategorie = await AssignCategoriesAsync(organizacja.CeleStatusowe);
                    
                    foreach (var kategoria in kategorie)
                    {
                        string insertKategoriaQuery = "INSERT INTO organizacjakategoria (numerkrs, kategoriaid) VALUES (@numerKrs, @kategoriaId)";
                        
                        await using var kategoriaCommand = new NpgsqlCommand(insertKategoriaQuery, connection, transaction);
                        kategoriaCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                        kategoriaCommand.Parameters.AddWithValue("@kategoriaId", kategoria.Id);
                        
                        await kategoriaCommand.ExecuteNonQueryAsync();
                    }
                }

                _logger.LogInformation("Successfully saved organizacja {NumerKrs} with {AdresCount} adresy, {KoordynatyCount} koordynaty, {KategorieCount} kategorie", 
                    organizacja.NumerKrs, organizacja.Adresy.Count, organizacja.Koordynaty.Count, organizacja.OrganizacjaKategorie?.Count ?? 0);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveOrganizacjaWithNpgsqlAsync for organizacja {NumerKrs}, rolling back transaction", organizacja.NumerKrs);
                await transaction.RollbackAsync();
                throw;
            }
        }

    }
}
