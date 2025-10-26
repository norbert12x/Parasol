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
        public string? AboutText { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Phone { get; set; }
        public string? ContactEmail { get; set; }
        public List<CategorySimpleDto> Categories { get; set; } = new List<CategorySimpleDto>();
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
    }

    public class UpdateProfileDto
    {
        public string? AboutText { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Phone { get; set; }
        public string? ContactEmail { get; set; }
        public List<int>? CategoryIds { get; set; }
        public List<int>? TagIds { get; set; }
    }

    public class OrganizationPublicProfileDto
    {
        public int Id { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public string? KrsNumber { get; set; }
        public string? AboutText { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Phone { get; set; }
        public string? ContactEmail { get; set; }
        public List<CategorySimpleDto> Categories { get; set; } = new List<CategorySimpleDto>();
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
    }
}
