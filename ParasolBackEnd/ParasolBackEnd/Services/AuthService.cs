using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using ParasolBackEnd.Data;
using ParasolBackEnd.DTOs;
using ParasolBackEnd.Models.MatchMaker;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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
        Task<bool> UpdateOrganizationProfileAsync(int organizationId, RegisterDto updateDto);
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

        public AuthService(SecondDbContext context, ILogger<AuthService> logger, IConfiguration configuration, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                // Sprawdź czy email już istnieje
                var existingOrganization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Email.ToLower() == registerDto.Email.ToLower());

                if (existingOrganization != null)
                {
                    _logger.LogWarning("Registration failed: Email {Email} already exists", registerDto.Email);
                    return null;
                }

                // Hashuj hasło
                var passwordHash = HashPassword(registerDto.Password);

                // Utwórz organizację
                var organization = new Organization
                {
                    Email = registerDto.Email.ToLower(),
                    PasswordHash = passwordHash,
                    OrganizationName = registerDto.OrganizationName,
                    KrsNumber = registerDto.KrsNumber
                };

                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Organization registered successfully: {Email}", registerDto.Email);

                // Wygeneruj token
                return GenerateAuthResponse(organization);
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
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Email.ToLower() == loginDto.Email.ToLower());

                if (organization == null)
                {
                    _logger.LogWarning("Login failed: Organization not found for email: {Email}", loginDto.Email);
                    return null;
                }

                if (!VerifyPassword(loginDto.Password, organization.PasswordHash))
                {
                    _logger.LogWarning("Login failed: Invalid password for email: {Email}", loginDto.Email);
                    return null;
                }

                _logger.LogInformation("Organization logged in successfully: {Email}", loginDto.Email);
                return GenerateAuthResponse(organization);
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
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == organizationId);

                if (organization == null)
                    return null;

                return new OrganizationProfileDto
                {
                    Id = organization.Id,
                    Email = organization.Email,
                    OrganizationName = organization.OrganizationName,
                    KrsNumber = organization.KrsNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization profile for organizationId: {OrganizationId}", organizationId);
                return null;
            }
        }

        public async Task<bool> UpdateOrganizationProfileAsync(int organizationId, RegisterDto updateDto)
        {
            try
            {
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == organizationId);

                if (organization == null)
                    return false;

                // Sprawdź czy email nie jest zajęty przez inną organizację
                if (updateDto.Email.ToLower() != organization.Email.ToLower())
                {
                    var existingOrganization = await _context.Organizations
                        .FirstOrDefaultAsync(o => o.Email.ToLower() == updateDto.Email.ToLower() && o.Id != organizationId);

                    if (existingOrganization != null)
                        return false;
                }

                // Aktualizuj dane organizacji
                organization.Email = updateDto.Email.ToLower();
                organization.OrganizationName = updateDto.OrganizationName;
                organization.KrsNumber = updateDto.KrsNumber;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Organization profile updated successfully: {OrganizationId}", organizationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organization profile for organizationId: {OrganizationId}", organizationId);
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(int organizationId, string currentPassword, string newPassword)
        {
            try
            {
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == organizationId);

                if (organization == null)
                    return false;

                if (!VerifyPassword(currentPassword, organization.PasswordHash))
                    return false;

                organization.PasswordHash = HashPassword(newPassword);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Password changed successfully for organizationId: {OrganizationId}", organizationId);
                return true;
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
                // Sprawdź czy email i KRS pasują do jednej organizacji
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Email.ToLower() == email.ToLower() && o.KrsNumber == krsNumber);

                if (organization == null)
                {
                    _logger.LogWarning("Password reset failed: Invalid email or KRS number for email: {Email}", email);
                    return null;
                }

                // Generuj krótki kod (6 znaków)
                var resetCode = GenerateShortCode();

                // Zapisz dane w cache na 24h
                var resetData = new PasswordResetData
                {
                    Email = organization.Email,
                    KrsNumber = organization.KrsNumber ?? string.Empty,
                    OrganizationId = organization.Id,
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

                // Znajdź organizację
                var organization = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == resetData.OrganizationId);

                if (organization == null)
                {
                    _logger.LogWarning("Organization not found for password reset with ID: {OrganizationId}", resetData.OrganizationId);
                    return false;
                }

                // Zaktualizuj hasło
                organization.PasswordHash = HashPassword(newPassword);
                await _context.SaveChangesAsync();

                // Usuń kod z cache (jednorazowe użycie)
                _cache.Remove($"password_reset_{code}");

                _logger.LogInformation("Password reset successfully for email: {Email}", resetData.Email);
                return true;
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
