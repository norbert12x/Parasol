using ParasolBackEnd.DTOs;
using ParasolBackEnd.Models.MatchMaker;

namespace ParasolBackEnd.Services
{
    public interface IMatchMakerService
    {
        Task<List<CategorySimpleDto>> GetCategoriesAsync();
        Task<CategoryDto?> GetCategoryByIdAsync(int id);
        Task<List<TagDto>> GetTagsAsync();
        Task<List<TagDto>> GetTagsByCategoryAsync(int categoryId);
        Task<TagDto?> GetTagByIdAsync(int id);
        
        // Post methods
        Task<List<PostDto>> GetPostsSummaryAsync(int? categoryId = null, int? tagId = null, string? searchTerm = null, 
            int page = 1, int pageSize = 20);
        Task<PostDto?> GetPostByIdAsync(int id, bool includeOrganization = true, bool includeCategories = true, bool includeTags = true);
        Task<PostDto> CreatePostAsync(CreatePostDto createPostDto);
        Task<PostDto?> UpdatePostAsync(int id, UpdatePostDto updatePostDto);
        Task<bool> DeletePostAsync(int id);
        Task<IEnumerable<PostDto>> GetPostsByOrganizationAsync(int organizationId, bool includeCategories = false, bool includeTags = false);
    }
}
