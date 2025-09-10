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
                        INSERT INTO organizations (email, password_hash, organization_name, krs_number)
                        VALUES (@email, @passwordHash, @organizationName, @krsNumber)
                        RETURNING id";

                    await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
                    insertCommand.Parameters.AddWithValue("@email", registerDto.Email.ToLower());
                    insertCommand.Parameters.AddWithValue("@passwordHash", passwordHash);
                    insertCommand.Parameters.AddWithValue("@organizationName", registerDto.OrganizationName);
                    insertCommand.Parameters.AddWithValue("@krsNumber", registerDto.KrsNumber);

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
                    SELECT id, email, password_hash, organization_name, krs_number
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
                        KrsNumber = reader.GetString(4)
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
                    SELECT id, email, organization_name, krs_number
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
                        KrsNumber = reader.GetString(3)
                    };

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
                    new Claim("OrganizationId", organization.Id.ToString())
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
