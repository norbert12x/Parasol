namespace ParasolBackEnd.DTOs
{
    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string? KrsNumber { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public int OrganizationId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class OrganizationProfileDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string? KrsNumber { get; set; }
    }
}
