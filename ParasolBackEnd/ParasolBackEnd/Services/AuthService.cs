using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using ParasolBackEnd.Data;
using ParasolBackEnd.DTOs;
using ParasolBackEnd.Models.MatchMaker;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace ParasolBackEnd.Services
{
    public class PasswordResetData
    {
        public string Email { get; set; } = string.Empty;
        public string KrsNumber { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto);
        Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
        Task<OrganizationProfileDto?> GetOrganizationProfileAsync(int organizationId);
        Task<bool> UpdateProfileAsync(int organizationId, UpdateProfileDto updateProfileDto);
        Task<bool> ChangePasswordAsync(int organizationId, string currentPassword, string newPassword);
        Task<string?> GeneratePasswordResetTokenAsync(string email, string krsNumber);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        bool ValidateToken(string token);
        int? GetOrganizationIdFromToken(string token);
    }

    public class AuthService : IAuthService
    {
        private readonly SecondDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly string _connectionString;

        public AuthService(SecondDbContext context, ILogger<AuthService> logger, IConfiguration configuration, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _cache = cache;
            _connectionString = configuration.GetConnectionString("SecondDb") + ";Multiplexing=false;Pooling=false";
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                _logger.LogInformation("Starting RegisterAsync for email: {Email}", registerDto.Email);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy email już istnieje
                    const string checkEmailSql = "SELECT id FROM organizations WHERE LOWER(email) = LOWER(@email)";
                    await using var checkCommand = new NpgsqlCommand(checkEmailSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@email", registerDto.Email);

                    var existingId = await checkCommand.ExecuteScalarAsync();
                    if (existingId != null)
                    {
                        _logger.LogWarning("Registration failed: Email {Email} already exists", registerDto.Email);
                        await transaction.RollbackAsync();
                        return null;
                    }

                    // Hashuj hasło
                    var passwordHash = HashPassword(registerDto.Password);

                    // Wstaw organizację
                    const string insertSql = @"
                        INSERT INTO organizations (email, password_hash, organization_name, krs_number, role)
                        VALUES (@email, @passwordHash, @organizationName, @krsNumber, @role)
                        RETURNING id";

                    await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
                    insertCommand.Parameters.AddWithValue("@email", registerDto.Email.ToLower());
                    insertCommand.Parameters.AddWithValue("@passwordHash", passwordHash);
                    insertCommand.Parameters.AddWithValue("@organizationName", registerDto.OrganizationName);
                    insertCommand.Parameters.AddWithValue("@krsNumber", registerDto.KrsNumber);
                    insertCommand.Parameters.AddWithValue("@role", "user");

                    var organizationId = (int)await insertCommand.ExecuteScalarAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Organization registered successfully: {Email} with ID {Id}", registerDto.Email, organizationId);

                    // Pobierz organizację dla odpowiedzi
                    var organization = new Organization
                    {
                        Id = organizationId,
                        Email = registerDto.Email.ToLower(),
                        OrganizationName = registerDto.OrganizationName,
                        KrsNumber = registerDto.KrsNumber
                    };

                    return GenerateAuthResponse(organization);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", registerDto.Email);
                return null;
            }
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("Starting LoginAsync for email: {Email}", loginDto.Email);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT id, email, password_hash, organization_name, krs_number, role
                    FROM organizations 
                    WHERE LOWER(email) = LOWER(@email)";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@email", loginDto.Email);

                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var organization = new Organization
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        PasswordHash = reader.GetString(2),
                        OrganizationName = reader.GetString(3),
                        KrsNumber = reader.GetString(4),
                        Role = reader.GetString(5)
                    };

                    if (!VerifyPassword(loginDto.Password, organization.PasswordHash))
                    {
                        _logger.LogWarning("Login failed: Invalid password for email: {Email}", loginDto.Email);
                        return null;
                    }

                    _logger.LogInformation("Organization logged in successfully: {Email}", loginDto.Email);
                    return GenerateAuthResponse(organization);
                }

                _logger.LogWarning("Login failed: Organization not found for email: {Email}", loginDto.Email);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", loginDto.Email);
                return null;
            }
        }

        public async Task<OrganizationProfileDto?> GetOrganizationProfileAsync(int organizationId)
        {
            try
            {
                _logger.LogInformation("Starting GetOrganizationProfileAsync for organization {Id}", organizationId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT id, email, organization_name, krs_number, role, about_text, website_url, phone, contact_email
                    FROM organizations 
                    WHERE id = @organizationId";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@organizationId", organizationId);

                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var profile = new OrganizationProfileDto
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        OrganizationName = reader.GetString(2),
                        KrsNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Role = reader.GetString(4),
                        AboutText = reader.IsDBNull(5) ? null : reader.GetString(5),
                        WebsiteUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Phone = reader.IsDBNull(7) ? null : reader.GetString(7),
                        ContactEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Categories = new List<CategorySimpleDto>(),
                        Tags = new List<TagDto>()
                    };

                    await reader.CloseAsync();

                    // Pobierz kategorie organizacji
                    const string categoriesSql = @"
                        SELECT c.id, c.name 
                        FROM organization_categories oc 
                        JOIN categories c ON oc.category_id = c.id 
                        WHERE oc.organization_id = @organizationId";
                    
                    await using var categoriesCommand = new NpgsqlCommand(categoriesSql, connection);
                    categoriesCommand.Parameters.AddWithValue("@organizationId", organizationId);
                    
                    await using var categoriesReader = await categoriesCommand.ExecuteReaderAsync();
                    while (await categoriesReader.ReadAsync())
                    {
                        profile.Categories.Add(new CategorySimpleDto
                        {
                            Id = categoriesReader.GetInt32(0),
                            Name = categoriesReader.GetString(1)
                        });
                    }
                    await categoriesReader.CloseAsync();

                    // Pobierz tagi organizacji
                    const string tagsSql = @"
                        SELECT t.id, t.name, t.category_id, c.name as category_name
                        FROM organization_tags ot 
                        JOIN tags t ON ot.tag_id = t.id 
                        JOIN categories c ON t.category_id = c.id
                        WHERE ot.organization_id = @organizationId";
                    
                    await using var tagsCommand = new NpgsqlCommand(tagsSql, connection);
                    tagsCommand.Parameters.AddWithValue("@organizationId", organizationId);
                    
                    await using var tagsReader = await tagsCommand.ExecuteReaderAsync();
                    while (await tagsReader.ReadAsync())
                    {
                        profile.Tags.Add(new TagDto
                        {
                            Id = tagsReader.GetInt32(0),
                            Name = tagsReader.GetString(1),
                            CategoryId = tagsReader.GetInt32(2),
                            CategoryName = tagsReader.GetString(3)
                        });
                    }
                    await tagsReader.CloseAsync();

                    _logger.LogInformation("Successfully loaded organization profile for ID {Id}", organizationId);
                    return profile;
                }

                _logger.LogWarning("Organization not found for ID {Id}", organizationId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization profile for organizationId: {OrganizationId}", organizationId);
                return null;
            }
        }

        public async Task<bool> UpdateProfileAsync(int organizationId, UpdateProfileDto updateProfileDto)
        {
            try
            {
                _logger.LogInformation("Starting UpdateProfileAsync for organization {Id}", organizationId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy organizacja istnieje
                    const string checkSql = "SELECT id FROM organizations WHERE id = @organizationId";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@organizationId", organizationId);

                    var exists = await checkCommand.ExecuteScalarAsync();
                    if (exists == null)
                    {
                        _logger.LogWarning("Organization not found for ID {Id}", organizationId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Aktualizuj profile fields (tylko nie-null wartości)
                    var updateFields = new List<string>();
                    var parameters = new List<NpgsqlParameter>();

                    if (updateProfileDto.AboutText != null)
                    {
                        updateFields.Add("about_text = @aboutText");
                        parameters.Add(new NpgsqlParameter("@aboutText", updateProfileDto.AboutText));
                    }

                    if (updateProfileDto.WebsiteUrl != null)
                    {
                        updateFields.Add("website_url = @websiteUrl");
                        parameters.Add(new NpgsqlParameter("@websiteUrl", updateProfileDto.WebsiteUrl));
                    }

                    if (updateProfileDto.Phone != null)
                    {
                        updateFields.Add("phone = @phone");
                        parameters.Add(new NpgsqlParameter("@phone", updateProfileDto.Phone));
                    }

                    if (updateProfileDto.ContactEmail != null)
                    {
                        updateFields.Add("contact_email = @contactEmail");
                        parameters.Add(new NpgsqlParameter("@contactEmail", updateProfileDto.ContactEmail));
                    }

                    if (updateFields.Any())
                    {
                        var updateSql = $"UPDATE organizations SET {string.Join(", ", updateFields)} WHERE id = @organizationId";
                        await using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
                        foreach (var param in parameters)
                        {
                            updateCommand.Parameters.Add(param);
                        }
                        updateCommand.Parameters.AddWithValue("@organizationId", organizationId);
                        await updateCommand.ExecuteNonQueryAsync();
                    }

                    // Walidacja kategorii jeśli podane
                    if (updateProfileDto.CategoryIds != null)
                    {
                        var categoryIds = updateProfileDto.CategoryIds;
                        if (categoryIds.Any())
                        {
                            const string categoryCheckSql = "SELECT COUNT(*) FROM categories WHERE id = ANY(@categoryIds)";
                            await using var categoryCheckCommand = new NpgsqlCommand(categoryCheckSql, connection, transaction);
                            categoryCheckCommand.Parameters.AddWithValue("@categoryIds", categoryIds.ToArray());
                            var categoryCount = (long)await categoryCheckCommand.ExecuteScalarAsync();
                            
                            if (categoryCount != categoryIds.Count)
                            {
                                _logger.LogWarning("Some categories not found");
                                await transaction.RollbackAsync();
                                return false;
                            }

                            // Usuń stare kategorie
                            const string deleteCategoriesSql = "DELETE FROM organization_categories WHERE organization_id = @organizationId";
                            await using var deleteCategoriesCommand = new NpgsqlCommand(deleteCategoriesSql, connection, transaction);
                            deleteCategoriesCommand.Parameters.AddWithValue("@organizationId", organizationId);
                            await deleteCategoriesCommand.ExecuteNonQueryAsync();

                            // Dodaj nowe kategorie
                            foreach (var categoryId in categoryIds)
                            {
                                const string insertCategorySql = "INSERT INTO organization_categories (organization_id, category_id) VALUES (@organizationId, @categoryId)";
                                await using var insertCategoryCommand = new NpgsqlCommand(insertCategorySql, connection, transaction);
                                insertCategoryCommand.Parameters.AddWithValue("@organizationId", organizationId);
                                insertCategoryCommand.Parameters.AddWithValue("@categoryId", categoryId);
                                await insertCategoryCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    // Walidacja tagów jeśli podane
                    if (updateProfileDto.TagIds != null)
                    {
                        var tagIds = updateProfileDto.TagIds;
                        if (tagIds.Any())
                        {
                            const string tagCheckSql = "SELECT COUNT(*) FROM tags WHERE id = ANY(@tagIds)";
                            await using var tagCheckCommand = new NpgsqlCommand(tagCheckSql, connection, transaction);
                            tagCheckCommand.Parameters.AddWithValue("@tagIds", tagIds.ToArray());
                            var tagCount = (long)await tagCheckCommand.ExecuteScalarAsync();
                            
                            if (tagCount != tagIds.Count)
                            {
                                _logger.LogWarning("Some tags not found");
                                await transaction.RollbackAsync();
                                return false;
                            }

                            // Usuń stare tagi
                            const string deleteTagsSql = "DELETE FROM organization_tags WHERE organization_id = @organizationId";
                            await using var deleteTagsCommand = new NpgsqlCommand(deleteTagsSql, connection, transaction);
                            deleteTagsCommand.Parameters.AddWithValue("@organizationId", organizationId);
                            await deleteTagsCommand.ExecuteNonQueryAsync();

                            // Dodaj nowe tagi
                            foreach (var tagId in tagIds)
                            {
                                const string insertTagSql = "INSERT INTO organization_tags (organization_id, tag_id) VALUES (@organizationId, @tagId)";
                                await using var insertTagCommand = new NpgsqlCommand(insertTagSql, connection, transaction);
                                insertTagCommand.Parameters.AddWithValue("@organizationId", organizationId);
                                insertTagCommand.Parameters.AddWithValue("@tagId", tagId);
                                await insertTagCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Profile updated successfully for organization {Id}", organizationId);
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for organizationId: {OrganizationId}", organizationId);
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(int organizationId, string currentPassword, string newPassword)
        {
            try
            {
                _logger.LogInformation("Starting ChangePasswordAsync for organization {Id}", organizationId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Pobierz organizację i sprawdź hasło
                    const string checkSql = @"
                        SELECT password_hash 
                        FROM organizations 
                        WHERE id = @organizationId";

                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@organizationId", organizationId);

                    var passwordHash = await checkCommand.ExecuteScalarAsync() as string;
                    if (passwordHash == null)
                    {
                        _logger.LogWarning("Organization not found for ID {Id}", organizationId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    if (!VerifyPassword(currentPassword, passwordHash))
                    {
                        _logger.LogWarning("Invalid current password for organization {Id}", organizationId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Zaktualizuj hasło
                    const string updateSql = @"
                        UPDATE organizations 
                        SET password_hash = @newPasswordHash 
                        WHERE id = @organizationId";

                    await using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
                    updateCommand.Parameters.AddWithValue("@newPasswordHash", HashPassword(newPassword));
                    updateCommand.Parameters.AddWithValue("@organizationId", organizationId);

                    await updateCommand.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Password changed successfully for organization {Id}", organizationId);
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for organizationId: {OrganizationId}", organizationId);
                return false;
            }
        }

        public async Task<string?> GeneratePasswordResetTokenAsync(string email, string krsNumber)
        {
            try
            {
                _logger.LogInformation("Starting GeneratePasswordResetTokenAsync for email: {Email}", email);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT id 
                    FROM organizations 
                    WHERE LOWER(email) = LOWER(@email) AND krs_number = @krsNumber";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@email", email);
                command.Parameters.AddWithValue("@krsNumber", krsNumber);

                var organizationId = await command.ExecuteScalarAsync();
                if (organizationId == null)
                {
                    _logger.LogWarning("Password reset failed: Invalid email or KRS number for email: {Email}", email);
                    return null;
                }

                // Generuj krótki kod (6 znaków)
                var resetCode = GenerateShortCode();

                // Zapisz dane w cache na 24h
                var resetData = new PasswordResetData
                {
                    Email = email,
                    KrsNumber = krsNumber,
                    OrganizationId = (int)organizationId,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                _cache.Set($"password_reset_{resetCode}", resetData, TimeSpan.FromHours(24));

                _logger.LogInformation("Password reset code generated for email: {Email}", email);
                return resetCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating password reset code for email: {Email}", email);
                return null;
            }
        }

        public async Task<bool> ResetPasswordAsync(string code, string newPassword)
        {
            try
            {
                _logger.LogInformation("Starting ResetPasswordAsync with code");

                // Pobierz dane z cache
                var resetData = _cache.Get<PasswordResetData>($"password_reset_{code}");
                if (resetData == null)
                {
                    _logger.LogWarning("Password reset failed: Invalid or expired code");
                    return false; // Kod nie istnieje lub wygasł
                }

                // Sprawdź czy nie wygasł
                if (resetData.ExpiresAt < DateTime.UtcNow)
                {
                    _cache.Remove($"password_reset_{code}");
                    _logger.LogWarning("Password reset failed: Code expired");
                    return false;
                }

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy organizacja istnieje
                    const string checkSql = "SELECT id FROM organizations WHERE id = @organizationId";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@organizationId", resetData.OrganizationId);

                    var exists = await checkCommand.ExecuteScalarAsync();
                    if (exists == null)
                    {
                        _logger.LogWarning("Organization not found for password reset with ID: {OrganizationId}", resetData.OrganizationId);
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Zaktualizuj hasło
                    const string updateSql = @"
                        UPDATE organizations 
                        SET password_hash = @newPasswordHash 
                        WHERE id = @organizationId";

                    await using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
                    updateCommand.Parameters.AddWithValue("@newPasswordHash", HashPassword(newPassword));
                    updateCommand.Parameters.AddWithValue("@organizationId", resetData.OrganizationId);

                    await updateCommand.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();

                    // Usuń kod z cache (jednorazowe użycie)
                    _cache.Remove($"password_reset_{code}");

                    _logger.LogInformation("Password reset successfully for email: {Email}", resetData.Email);
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password with code");
                return false;
            }
        }

        public bool ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "default-key-change-in-production");

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"] ?? "ParasolBackEnd",
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"] ?? "ParasolFrontEnd",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public int? GetOrganizationIdFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "default-key-change-in-production");

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"] ?? "ParasolBackEnd",
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"] ?? "ParasolFrontEnd",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);
                var organizationIdClaim = principal.FindFirst("OrganizationId")?.Value;

                return organizationIdClaim != null ? int.Parse(organizationIdClaim) : null;
            }
            catch
            {
                return null;
            }
        }

        private AuthResponseDto GenerateAuthResponse(Organization organization)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "default-key-change-in-production");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, organization.Id.ToString()),
                    new Claim(ClaimTypes.Email, organization.Email),
                    new Claim(ClaimTypes.Name, organization.OrganizationName),
                    new Claim("OrganizationId", organization.Id.ToString()),
                    new Claim(ClaimTypes.Role, organization.Role)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _configuration["Jwt:Issuer"] ?? "ParasolBackEnd",
                Audience = _configuration["Jwt:Audience"] ?? "ParasolFrontEnd",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return new AuthResponseDto
            {
                Token = tokenHandler.WriteToken(token),
                Email = organization.Email,
                OrganizationName = organization.OrganizationName,
                OrganizationId = organization.Id,
                Role = organization.Role,
                ExpiresAt = tokenDescriptor.Expires ?? DateTime.UtcNow.AddDays(7)
            };
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            var hashedPassword = HashPassword(password);
            return hashedPassword == hash;
        }

        private string GenerateShortCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
