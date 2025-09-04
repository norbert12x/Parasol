using Microsoft.AspNetCore.Mvc;
using ParasolBackEnd.DTOs;
using ParasolBackEnd.Services;

namespace ParasolBackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Walidacja danych
                if (string.IsNullOrWhiteSpace(registerDto.Email) || string.IsNullOrWhiteSpace(registerDto.Password))
                {
                    return BadRequest("Email i hasło są wymagane");
                }

                if (string.IsNullOrWhiteSpace(registerDto.OrganizationName))
                {
                    return BadRequest("Nazwa organizacji jest wymagana");
                }

                if (registerDto.Password.Length < 6)
                {
                    return BadRequest("Hasło musi mieć co najmniej 6 znaków");
                }

                var result = await _authService.RegisterAsync(registerDto);
                
                if (result == null)
                {
                    return BadRequest("Organizacja z tym adresem email już istnieje");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, "Wystąpił błąd podczas rejestracji");
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                {
                    return BadRequest("Email i hasło są wymagane");
                }

                var result = await _authService.LoginAsync(loginDto);
                
                if (result == null)
                {
                    return Unauthorized("Nieprawidłowy email lub hasło");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, "Wystąpił błąd podczas logowania");
            }
        }

        [HttpGet("profile")]
        public async Task<ActionResult<OrganizationProfileDto>> GetProfile()
        {
            try
            {
                // Pobierz token z nagłówka Authorization
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zobaczyć profil", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                
                if (!_authService.ValidateToken(token))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zobaczyć profil", requiresAuth = true });
                }

                var organizationId = _authService.GetOrganizationIdFromToken(token);
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zobaczyć profil", requiresAuth = true });
                }

                var profile = await _authService.GetOrganizationProfileAsync(organizationId.Value);
                
                if (profile == null)
                {
                    return NotFound("Profil organizacji nie został znaleziony");
                }

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile");
                return StatusCode(500, "Wystąpił błąd podczas pobierania profilu");
            }
        }

        [HttpPut("profile")]
        public async Task<ActionResult> UpdateProfile([FromBody] RegisterDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Pobierz token z nagłówka Authorization
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zaktualizować profil", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                
                if (!_authService.ValidateToken(token))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zaktualizować profil", requiresAuth = true });
                }

                var organizationId = _authService.GetOrganizationIdFromToken(token);
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zaktualizować profil", requiresAuth = true });
                }

                var success = await _authService.UpdateOrganizationProfileAsync(organizationId.Value, updateDto);
                
                if (!success)
                {
                    return BadRequest("Nie udało się zaktualizować profilu lub email jest już zajęty");
                }

                return Ok(new { message = "Profil został zaktualizowany" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, "Wystąpił błąd podczas aktualizacji profilu");
            }
        }

        [HttpPost("change-password")]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(changePasswordDto.CurrentPassword) || string.IsNullOrWhiteSpace(changePasswordDto.NewPassword))
                {
                    return BadRequest("Obecne i nowe hasło są wymagane");
                }

                if (changePasswordDto.NewPassword.Length < 6)
                {
                    return BadRequest("Nowe hasło musi mieć co najmniej 6 znaków");
                }

                // Pobierz token z nagłówka Authorization
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zmienić hasło", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                
                if (!_authService.ValidateToken(token))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zmienić hasło", requiresAuth = true });
                }

                var organizationId = _authService.GetOrganizationIdFromToken(token);
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zmienić hasło", requiresAuth = true });
                }

                var success = await _authService.ChangePasswordAsync(organizationId.Value, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
                
                if (!success)
                {
                    return BadRequest("Nieprawidłowe obecne hasło");
                }

                return Ok(new { message = "Hasło zostało zmienione" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, "Wystąpił błąd podczas zmiany hasła");
            }
        }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
