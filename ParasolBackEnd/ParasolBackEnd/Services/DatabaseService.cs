using Microsoft.EntityFrameworkCore;
using ParasolBackEnd.Data;
using ParasolBackEnd.Models;
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
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(AppDbContext context, ILogger<DatabaseService> logger)
        {
            _context = context;
            _logger = logger;
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
            _logger.LogInformation("Attempting to get kategorie using direct Npgsql connection");
            
            try
            {
                var connectionString = "User Id=postgres.roaifaijrauldhgxpdrl;Password=symi4MuiLt*Gybky!kJ!;Server=aws-1-eu-central-1.pooler.supabase.com;Port=6543;Database=postgres;Multiplexing=false;Pooling=false";
                
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                
                string query = "SELECT id, nazwa FROM kategoria ORDER BY id";
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
                
                _logger.LogInformation("Direct Npgsql query returned {Count} kategorie from database", kategorie.Count);
                
                // Log each kategoria for debugging
                foreach (var kategoria in kategorie)
                {
                    _logger.LogInformation("Kategoria: ID={Id}, Nazwa={Nazwa}", kategoria.Id, kategoria.Nazwa);
                }
                
                return kategorie;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting kategorie using direct Npgsql: {ErrorDetails}", ex.ToString());
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
                    .FirstOrDefaultAsync(o => o.NumerKrs == organizacja.NumerKrs);

                if (existing == null)
                {
                    _logger.LogWarning("Organizacja with KRS {NumerKrs} not found for update", organizacja.NumerKrs);
                    return false;
                }

                existing.Nazwa = organizacja.Nazwa;
                // Można dodać więcej pól do aktualizacji

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Organizacja updated successfully: {NumerKrs}", organizacja.NumerKrs);
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

                var importedCount = 0;
                var errors = new List<string>();

                foreach (var organizacja in organizacje)
                {
                    try
                    {
                        _logger.LogInformation("Processing organizacja {Krs}: \"{Nazwa}\"", organizacja.NumerKrs, organizacja.Nazwa);

                        // Sprawdź czy organizacja już istnieje
                        if (await OrganizacjaExistsAsync(organizacja.NumerKrs))
                        {
                            _logger.LogInformation("Organizacja {Krs} already exists, UPDATING instead of skipping", organizacja.NumerKrs);
                            // UPDATE istniejącej organizacji zamiast pomijania
                        }
                        else
                        {
                            _logger.LogInformation("Organizacja {Krs} is new, will INSERT", organizacja.NumerKrs);
                        }

                        _logger.LogInformation("Saving organizacja {Krs} to database", organizacja.NumerKrs);

                        // Użyj bezpośrednio Npgsql zamiast Entity Framework
                        await SaveOrganizacjaWithNpgsqlAsync(organizacja);
                        
                        importedCount++;
                        _logger.LogInformation("Successfully imported/updated organizacja {Krs}", organizacja.NumerKrs);
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error importing organizacja {organizacja.NumerKrs}: {ex.Message}";
                        _logger.LogError(ex, errorMessage);
                        errors.Add(errorMessage);
                    }
                }

                _logger.LogInformation("Import completed. Imported: {ImportedCount}, Errors: {ErrorCount}", importedCount, errors.Count);
                return new ImportResult { ImportedCount = importedCount, Errors = errors };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during import process");
                return new ImportResult { ImportedCount = 0, Errors = new List<string> { ex.Message } };
            }
        }

        private async Task<List<Organizacja>> GetOrganizacjeFromGeolokalizacjaAsync(string? wojewodztwo = null)
        {
            try
            {
                var url = "https://localhost:7170/api/OrganizacjeGeolokalizacja";
                if (!string.IsNullOrEmpty(wojewodztwo))
                {
                    url += $"?wojewodztwo={Uri.EscapeDataString(wojewodztwo)}";
                }

                _logger.LogInformation("Fetching data from: {Url}", url);

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

                _logger.LogInformation("Deserialized {Count} organizacje from geolokalizacja endpoint", organizacjeDto.Count);

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
                            Kraj = a.Kraj
                        }).ToList(),
                        Koordynaty = dto.Koordynaty.Select(k => new Koordynaty
                        {
                            NumerKrs = k.NumerKrs,
                            Latitude = k.Latitude,
                            Longitude = k.Longitude
                        }).ToList()
                    };

                    organizacje.Add(organizacja);
                    
                    _logger.LogInformation("Mapped organizacja {Krs}: {Nazwa} with {AdresCount} adresy and {KoordynatyCount} koordynaty", 
                        organizacja.NumerKrs, organizacja.Nazwa, organizacja.Adresy.Count, organizacja.Koordynaty.Count);
                }

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
                _logger.LogInformation("Starting category assignment for organizacja with CeleStatusowe: {CeleStatusowe}", string.Join(", ", celeStatusowe));
                
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
                
                _logger.LogInformation("Assigned {Count} categories to organizacja with CeleStatusowe: {CeleStatusowe}", 
                    assignedCategories.Count, string.Join(", ", celeStatusowe));
                
                return assignedCategories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning categories to organizacja with CeleStatusowe: {ErrorDetails}", 
                    string.Join(", ", celeStatusowe), ex.ToString());
                throw; // Przekaż błąd dalej
            }
        }

        private async Task<bool> OrganizacjaExistsAsync(string numerKrs)
        {
            var connectionString = "User Id=postgres.roaifaijrauldhgxpdrl;Password=symi4MuiLt*Gybky!kJ!;Server=aws-1-eu-central-1.pooler.supabase.com;Port=6543;Database=postgres;Multiplexing=false;Pooling=false";
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            string query = "SELECT COUNT(*) FROM organizacja WHERE numerkrs = @numerKrs";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@numerKrs", numerKrs);
            
            var count = await command.ExecuteScalarAsync();
            return Convert.ToInt32(count) > 0;
        }

        private async Task SaveOrganizacjaWithNpgsqlAsync(Organizacja organizacja)
        {
            _logger.LogInformation("Starting SaveOrganizacjaWithNpgsqlAsync for organizacja {NumerKrs}", organizacja.NumerKrs);
            
            var connectionString = "User Id=postgres.roaifaijrauldhgxpdrl;Password=symi4MuiLt*Gybky!kJ!;Server=aws-1-eu-central-1.pooler.supabase.com;Port=6543;Database=postgres;Multiplexing=false;Pooling=false";
            
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            _logger.LogInformation("Database connection opened successfully");
            
            await using var transaction = await connection.BeginTransactionAsync();
            _logger.LogInformation("Transaction started");
            
            try
            {
                // Sprawdź czy organizacja istnieje
                bool organizacjaExists = await OrganizacjaExistsAsync(organizacja.NumerKrs);
                
                if (organizacjaExists)
                {
                    // 1. UPDATE istniejącej organizacji
                    _logger.LogInformation("Step 1: UPDATING existing organizacja {NumerKrs} in organizacja table", organizacja.NumerKrs);
                    string updateOrgQuery = "UPDATE organizacja SET nazwa = @nazwa WHERE numerkrs = @numerKrs";
                    await using var orgCommand = new NpgsqlCommand(updateOrgQuery, connection, transaction);
                    orgCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    orgCommand.Parameters.AddWithValue("@nazwa", organizacja.Nazwa);
                    await orgCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("Step 1 COMPLETED: Organizacja {NumerKrs} UPDATED successfully", organizacja.NumerKrs);
                    
                    // Usuń stare adresy i koordynaty przed wstawieniem nowych
                    _logger.LogInformation("Deleting old adresy and koordynaty for organizacja {NumerKrs}", organizacja.NumerKrs);
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
                    
                    _logger.LogInformation("Old data deleted for organizacja {NumerKrs}", organizacja.NumerKrs);
                }
                else
                {
                    // 1. INSERT nowej organizacji
                    _logger.LogInformation("Step 1: INSERTING new organizacja {NumerKrs} into organizacja table", organizacja.NumerKrs);
                    string insertOrgQuery = "INSERT INTO organizacja (numerkrs, nazwa) VALUES (@numerKrs, @nazwa)";
                    await using var orgCommand = new NpgsqlCommand(insertOrgQuery, connection, transaction);
                    orgCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                    orgCommand.Parameters.AddWithValue("@nazwa", organizacja.Nazwa);
                    await orgCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("Step 1 COMPLETED: Organizacja {NumerKrs} INSERTED successfully", organizacja.NumerKrs);
                }

                // 2. POTEM zapisz adresy (po wstawieniu/update organizacji)
                _logger.LogInformation("Step 2: Inserting {Count} adresy for organizacja {NumerKrs}", organizacja.Adresy.Count, organizacja.NumerKrs);
                foreach (var adres in organizacja.Adresy)
                {
                    // Upewnij się, że adres ma poprawny numerkrs
                    adres.NumerKrs = organizacja.NumerKrs;
                    
                    _logger.LogInformation("Inserting adres: Ulica={Ulica}, Miejscowosc={Miejscowosc}, NumerKrs={NumerKrs}", 
                        adres.Ulica, adres.Miejscowosc, adres.NumerKrs);
                    
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
                    _logger.LogInformation("Adres inserted successfully for organizacja {NumerKrs}", organizacja.NumerKrs);
                }
                _logger.LogInformation("Step 2 COMPLETED: All adresy inserted successfully");

                // 3. POTEM zapisz koordynaty (po wstawieniu/update organizacji)
                _logger.LogInformation("Step 3: Inserting {Count} koordynaty for organizacja {NumerKrs}", organizacja.Koordynaty.Count, organizacja.NumerKrs);
                foreach (var koordynaty in organizacja.Koordynaty)
                {
                    // Upewnij się, że koordynaty mają poprawny numerkrs
                    koordynaty.NumerKrs = organizacja.NumerKrs;
                    
                    _logger.LogInformation("Inserting koordynaty: Lat={Latitude}, Lon={Longitude}, NumerKrs={NumerKrs}", 
                        koordynaty.Latitude, koordynaty.Longitude, koordynaty.NumerKrs);
                    
                    string insertKoordynatyQuery = "INSERT INTO koordynaty (numerkrs, latitude, longitude) VALUES (@numerKrs, @latitude, @longitude)";
                    
                    await using var koordynatyCommand = new NpgsqlCommand(insertKoordynatyQuery, connection, transaction);
                    koordynatyCommand.Parameters.AddWithValue("@numerKrs", koordynaty.NumerKrs);
                    koordynatyCommand.Parameters.AddWithValue("@latitude", koordynaty.Latitude);
                    koordynatyCommand.Parameters.AddWithValue("@longitude", koordynaty.Longitude);
                    
                    await koordynatyCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("Koordynaty inserted successfully for organizacja {NumerKrs}", organizacja.NumerKrs);
                }
                _logger.LogInformation("Step 3 COMPLETED: All koordynaty inserted successfully");

                // 4. NA KONIEC przypisz kategorie (po wstawieniu/update organizacji)
                if (organizacja.CeleStatusowe?.Any() == true)
                {
                    _logger.LogInformation("Step 4: Assigning categories for organizacja {NumerKrs} with {Count} CeleStatusowe", 
                        organizacja.NumerKrs, organizacja.CeleStatusowe.Count);
                    
                    var kategorie = await AssignCategoriesAsync(organizacja.CeleStatusowe);
                    _logger.LogInformation("Found {Count} matching categories for organizacja {NumerKrs}", kategorie.Count, organizacja.NumerKrs);
                    
                    foreach (var kategoria in kategorie)
                    {
                        _logger.LogInformation("Inserting category {KategoriaId} ({KategoriaNazwa}) for organizacja {NumerKrs}", 
                            kategoria.Id, kategoria.Nazwa, organizacja.NumerKrs);
                        
                        string insertKategoriaQuery = "INSERT INTO organizacjakategoria (numerkrs, kategoriaid) VALUES (@numerKrs, @kategoriaId)";
                        
                        await using var kategoriaCommand = new NpgsqlCommand(insertKategoriaQuery, connection, transaction);
                        kategoriaCommand.Parameters.AddWithValue("@numerKrs", organizacja.NumerKrs);
                        kategoriaCommand.Parameters.AddWithValue("@kategoriaId", kategoria.Id);
                        
                        await kategoriaCommand.ExecuteNonQueryAsync();
                        _logger.LogInformation("Category {KategoriaId} assigned successfully to organizacja {NumerKrs}", kategoria.Id, organizacja.NumerKrs);
                    }
                    _logger.LogInformation("Step 4 COMPLETED: All categories assigned successfully");
                }
                else
                {
                    _logger.LogInformation("Step 4 SKIPPED: No CeleStatusowe for organizacja {NumerKrs}", organizacja.NumerKrs);
                }

                _logger.LogInformation("All steps completed successfully, committing transaction for organizacja {NumerKrs}", organizacja.NumerKrs);
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully for organizacja {NumerKrs}", organizacja.NumerKrs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveOrganizacjaWithNpgsqlAsync for organizacja {NumerKrs}, rolling back transaction", organizacja.NumerKrs);
                await transaction.RollbackAsync();
                _logger.LogInformation("Transaction rolled back for organizacja {NumerKrs}", organizacja.NumerKrs);
                throw;
            }
        }
    }
}
