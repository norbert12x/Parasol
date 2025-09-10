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

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(forgotPasswordDto.Email) || string.IsNullOrWhiteSpace(forgotPasswordDto.KrsNumber))
                {
                    return BadRequest("Email i numer KRS są wymagane");
                }

                var resetToken = await _authService.GeneratePasswordResetTokenAsync(forgotPasswordDto.Email, forgotPasswordDto.KrsNumber);
                
                if (resetToken == null)
                {
                    return BadRequest("Nieprawidłowy email lub numer KRS");
                }

                // Zwracamy krótki kod do frontendu (hybrydowe rozwiązanie)
                return Ok(new { 
                    message = "Jeśli podane dane są poprawne, wysłaliśmy email z instrukcjami resetowania hasła",
                    resetCode = resetToken,
                    success = true 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password");
                return StatusCode(500, "Wystąpił błąd podczas resetowania hasła");
            }
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(resetPasswordDto.Token) || string.IsNullOrWhiteSpace(resetPasswordDto.NewPassword))
                {
                    return BadRequest("Kod i nowe hasło są wymagane");
                }

                if (resetPasswordDto.NewPassword.Length < 6)
                {
                    return BadRequest("Nowe hasło musi mieć co najmniej 6 znaków");
                }

                if (resetPasswordDto.NewPassword != resetPasswordDto.ConfirmPassword)
                {
                    return BadRequest("Hasła nie są identyczne");
                }

                var success = await _authService.ResetPasswordAsync(resetPasswordDto.Token, resetPasswordDto.NewPassword);
                
                if (!success)
                {
                    return BadRequest("Nieprawidłowy lub wygasły kod");
                }

                return Ok(new { message = "Hasło zostało zresetowane pomyślnie" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                return StatusCode(500, "Wystąpił błąd podczas resetowania hasła");
            }
        }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
