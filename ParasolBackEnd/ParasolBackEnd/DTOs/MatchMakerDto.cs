namespace ParasolBackEnd.DTOs
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
    }

    public class CategorySimpleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TagDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class PostDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateOnly? ExpiresAt { get; set; }
        public int OrganizationId { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public List<CategorySimpleDto> Categories { get; set; } = new List<CategorySimpleDto>();
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
    }

    public class CreatePostDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public DateOnly? ExpiresAt { get; set; }
        public int OrganizationId { get; set; }
        public List<int> CategoryIds { get; set; } = new List<int>();
        public List<int> TagIds { get; set; } = new List<int>();
    }

    public class UpdatePostDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateOnly? ExpiresAt { get; set; }
        public List<int> CategoryIds { get; set; } = new List<int>();
        public List<int> TagIds { get; set; } = new List<int>();
    }

    public class ForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
        public string KrsNumber { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
