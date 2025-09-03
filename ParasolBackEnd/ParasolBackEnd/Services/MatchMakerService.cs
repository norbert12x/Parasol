using Microsoft.EntityFrameworkCore;
using ParasolBackEnd.Data;
using ParasolBackEnd.DTOs;
using ParasolBackEnd.Models.MatchMaker;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Memory;

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
        Task<List<PostDto>> GetPostsAsync(int? categoryId = null, int? tagId = null, string? searchTerm = null, 
            bool includeOrganization = true, bool includeCategories = true, bool includeTags = true, 
            int page = 1, int pageSize = 20);
        Task<List<PostDto>> GetPostsSummaryAsync(int? categoryId = null, int? tagId = null, string? searchTerm = null, 
            int page = 1, int pageSize = 20);
        Task<PostDto?> GetPostByIdAsync(int id, bool includeOrganization = true, bool includeCategories = true, bool includeTags = true);
        Task<PostDto> CreatePostAsync(CreatePostDto createPostDto);
        Task<PostDto?> UpdatePostAsync(int id, UpdatePostDto updatePostDto);
        Task<bool> DeletePostAsync(int id);
        Task ClearCacheAsync();
    }

    public class MatchMakerService : IMatchMakerService
    {
        private readonly SecondDbContext _context;
        private readonly ILogger<MatchMakerService> _logger;
        private readonly IMemoryCache _cache;

        public MatchMakerService(SecondDbContext context, ILogger<MatchMakerService> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        public async Task<List<CategorySimpleDto>> GetCategoriesAsync()
        {
            try
            {
                const string cacheKey = "categories_simple";
                
                if (_cache.TryGetValue(cacheKey, out List<CategorySimpleDto>? cachedCategories))
                {
                    return cachedCategories ?? new List<CategorySimpleDto>();
                }

                var categories = await _context.Categories
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var result = categories.Select(MapToCategorySimpleDto).ToList();
                
                // Cache na 30 minut
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return new List<CategorySimpleDto>();
            }
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Tags)
                    .FirstOrDefaultAsync(c => c.Id == id);

                return category != null ? MapToCategoryDto(category) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category by id: {Id}", id);
                return null;
            }
        }

        public async Task<List<TagDto>> GetTagsAsync()
        {
            try
            {
                const string cacheKey = "tags_all";
                
                if (_cache.TryGetValue(cacheKey, out List<TagDto>? cachedTags))
                {
                    return cachedTags ?? new List<TagDto>();
                }

                var tags = await _context.Tags
                    .Include(t => t.Category)
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                var result = tags.Select(MapToTagDto).ToList();
                
                // Cache na 30 minut
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags");
                return new List<TagDto>();
            }
        }

        public async Task<List<TagDto>> GetTagsByCategoryAsync(int categoryId)
        {
            try
            {
                var tags = await _context.Tags
                    .Include(t => t.Category)
                    .Where(t => t.CategoryId == categoryId)
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                return tags.Select(MapToTagDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags by category: {CategoryId}", categoryId);
                return new List<TagDto>();
            }
        }

        public async Task<TagDto?> GetTagByIdAsync(int id)
        {
            try
            {
                var tag = await _context.Tags
                    .Include(t => t.Category)
                    .FirstOrDefaultAsync(t => t.Id == id);

                return tag != null ? MapToTagDto(tag) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag by id: {Id}", id);
                return null;
            }
        }

        private static CategoryDto MapToCategoryDto(Category category)
        {
            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Tags = category.Tags.Select(MapToTagDto).ToList()
            };
        }

        private static CategorySimpleDto MapToCategorySimpleDto(Category category)
        {
            return new CategorySimpleDto
            {
                Id = category.Id,
                Name = category.Name
            };
        }

        private static TagDto MapToTagDto(Tag tag)
        {
            return new TagDto
            {
                Id = tag.Id,
                Name = tag.Name,
                CategoryId = tag.CategoryId,
                CategoryName = tag.Category?.Name ?? string.Empty
            };
        }

        // Post methods implementation
        public async Task<List<PostDto>> GetPostsAsync(int? categoryId = null, int? tagId = null, string? searchTerm = null, 
            bool includeOrganization = true, bool includeCategories = true, bool includeTags = true, 
            int page = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.Posts.AsQueryable();

                // Selektywne ładowanie relacji tylko gdy potrzebne
                if (includeOrganization)
                {
                    query = query.Include(p => p.Organization);
                }

                if (includeCategories)
                {
                    query = query.Include(p => p.PostCategories)
                        .ThenInclude(pc => pc.Category);
                }

                if (includeTags)
                {
                    query = query.Include(p => p.PostTags)
                        .ThenInclude(pt => pt.Tag)
                            .ThenInclude(t => t.Category);
                }

                // Apply filters
                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.PostCategories.Any(pc => pc.CategoryId == categoryId.Value));
                }

                if (tagId.HasValue)
                {
                    query = query.Where(p => p.PostTags.Any(pt => pt.TagId == tagId.Value));
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(p => p.Title.Contains(searchTerm) || 
                                           p.Description.Contains(searchTerm));
                }

                // Paginacja
                var posts = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return posts.Select(post => MapToPostDto(post, includeOrganization, includeCategories, includeTags)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts");
                return new List<PostDto>();
            }
        }

        public async Task<List<PostDto>> GetPostsSummaryAsync(int? categoryId = null, int? tagId = null, string? searchTerm = null, 
            int page = 1, int pageSize = 20)
        {
            try
            {
                // Cache tylko dla podstawowych zapytań bez filtrów
                if (!categoryId.HasValue && !tagId.HasValue && string.IsNullOrWhiteSpace(searchTerm))
                {
                    var cacheKey = $"posts_summary_page_{page}_size_{pageSize}";
                    
                    if (_cache.TryGetValue(cacheKey, out List<PostDto>? cachedPosts))
                    {
                        return cachedPosts ?? new List<PostDto>();
                    }
                }

                var query = _context.Posts.AsQueryable();

                // Selektywne ładowanie relacji tylko gdy potrzebne
                query = query.Include(p => p.Organization);
                query = query.Include(p => p.PostCategories)
                    .ThenInclude(pc => pc.Category);
                query = query.Include(p => p.PostTags)
                    .ThenInclude(pt => pt.Tag)
                        .ThenInclude(t => t.Category);

                // Apply filters
                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.PostCategories.Any(pc => pc.CategoryId == categoryId.Value));
                }

                if (tagId.HasValue)
                {
                    query = query.Where(p => p.PostTags.Any(pt => pt.TagId == tagId.Value));
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(p => p.Title.Contains(searchTerm) || 
                                           p.Description.Contains(searchTerm));
                }

                // Paginacja
                var posts = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = posts.Select(post => MapToPostDto(post, true, true, true)).ToList();
                
                // Cache tylko dla podstawowych zapytań
                if (!categoryId.HasValue && !tagId.HasValue && string.IsNullOrWhiteSpace(searchTerm))
                {
                    var cacheKey = $"posts_summary_page_{page}_size_{pageSize}";
                    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5)); // Krótszy cache dla postów
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts summary");
                return new List<PostDto>();
            }
        }

        public async Task<PostDto?> GetPostByIdAsync(int id, bool includeOrganization = true, bool includeCategories = true, bool includeTags = true)
        {
            try
            {
                var query = _context.Posts.AsQueryable();

                // Selektywne ładowanie relacji
                if (includeOrganization)
                {
                    query = query.Include(p => p.Organization);
                }

                if (includeCategories)
                {
                    query = query.Include(p => p.PostCategories)
                        .ThenInclude(pc => pc.Category);
                }

                if (includeTags)
                {
                    query = query.Include(p => p.PostTags)
                        .ThenInclude(pt => pt.Tag)
                            .ThenInclude(t => t.Category);
                }

                var post = await query.FirstOrDefaultAsync(p => p.Id == id);

                return post != null ? MapToPostDto(post, includeOrganization, includeCategories, includeTags) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post by id: {Id}", id);
                return null;
            }
        }

        public async Task<PostDto> CreatePostAsync(CreatePostDto createPostDto)
        {
            try
            {
                // Parse ExpiresAt
                DateOnly? expiresAt = createPostDto.ExpiresAt;

                // Walidacja organizacji
                var organization = await _context.Organizations.FindAsync(createPostDto.OrganizationId);
                if (organization == null)
                {
                    throw new ArgumentException($"Organization with ID {createPostDto.OrganizationId} not found");
                }

                // Walidacja kategorii
                var categoryIds = createPostDto.CategoryIds ?? new List<int>();
                if (categoryIds.Any())
                {
                    await ValidateIdsAsync(categoryIds, _context.Categories, "Categories");
                }

                // Walidacja tagów
                var tagIds = createPostDto.TagIds ?? new List<int>();
                if (tagIds.Any())
                {
                    await ValidateIdsAsync(tagIds, _context.Tags, "Tags");
                }

                var post = new Post
                {
                    Title = createPostDto.Title,
                    Description = createPostDto.Description,
                    ContactEmail = createPostDto.ContactEmail,
                    ContactPhone = createPostDto.ContactPhone,
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    OrganizationId = createPostDto.OrganizationId
                };

                // Użyj transakcji dla wszystkich operacji
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        _context.Posts.Add(post);
                        await _context.SaveChangesAsync();

                        // Add categories
                        foreach (var categoryId in categoryIds)
                        {
                            var postCategory = new PostCategory
                            {
                                PostId = post.Id,
                                CategoryId = categoryId
                            };
                            _context.PostCategories.Add(postCategory);
                        }

                        // Add tags
                        foreach (var tagId in tagIds)
                        {
                            var postTag = new PostTag
                            {
                                PostId = post.Id,
                                TagId = tagId
                            };
                            _context.PostTags.Add(postTag);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Successfully created post with ID {PostId}, Title: {Title}, Categories: {CategoryCount}, Tags: {TagCount}", 
                            post.Id, post.Title, categoryIds.Count, tagIds.Count);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                // Return the created post
                var result = await GetPostByIdAsync(post.Id) ?? new PostDto();
                
                // Wyczyść cache po dodaniu nowego posta
                _cache.Remove("posts_summary");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                throw;
            }
        }

        public async Task<PostDto?> UpdatePostAsync(int id, UpdatePostDto updatePostDto)
        {
            try
            {
                var post = await _context.Posts
                    .Include(p => p.PostCategories)
                    .Include(p => p.PostTags)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                    return null;

                // Walidacja kategorii
                var categoryIds = updatePostDto.CategoryIds ?? new List<int>();
                if (categoryIds.Any())
                {
                    await ValidateIdsAsync(categoryIds, _context.Categories, "Categories");
                }

                // Walidacja tagów
                var tagIds = updatePostDto.TagIds ?? new List<int>();
                if (tagIds.Any())
                {
                    await ValidateIdsAsync(tagIds, _context.Tags, "Tags");
                }

                // Użyj transakcji dla wszystkich operacji
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Update basic properties
                        post.Title = updatePostDto.Title;
                        post.Description = updatePostDto.Description;
                        post.ContactEmail = updatePostDto.ContactEmail;
                        post.ContactPhone = updatePostDto.ContactPhone;
                        post.Status = updatePostDto.Status;
                        post.UpdatedAt = DateTime.UtcNow;
                        post.ExpiresAt = updatePostDto.ExpiresAt;

                        // Update categories
                        _context.PostCategories.RemoveRange(post.PostCategories);
                        foreach (var categoryId in categoryIds)
                        {
                            var postCategory = new PostCategory
                            {
                                PostId = post.Id,
                                CategoryId = categoryId
                            };
                            _context.PostCategories.Add(postCategory);
                        }

                        // Update tags
                        _context.PostTags.RemoveRange(post.PostTags);
                        foreach (var tagId in tagIds)
                        {
                            var postTag = new PostTag
                            {
                                PostId = post.Id,
                                TagId = tagId
                            };
                            _context.PostTags.Add(postTag);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Successfully updated post with ID {PostId}, Title: {Title}, Categories: {CategoryCount}, Tags: {TagCount}", 
                            post.Id, post.Title, categoryIds.Count, tagIds.Count);
                        
                        // Wyczyść cache po aktualizacji posta
                        _cache.Remove("posts_summary");
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return await GetPostByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post with id: {Id}", id);
                return null;
            }
        }

        public async Task<bool> DeletePostAsync(int id)
        {
            try
            {
                var post = await _context.Posts.FindAsync(id);
                if (post == null)
                {
                    return false;
                }

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();
                
                // Wyczyść cache po usunięciu posta
                _cache.Remove("posts_summary");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post with ID {PostId}", id);
                return false;
            }
        }

        public async Task ClearCacheAsync()
        {
            try
            {
                _cache.Remove("categories_simple");
                _cache.Remove("tags_all");
                _cache.Remove("posts_summary");
                
                // Usuń wszystkie klucze cache związane z postami (prostsze podejście)
                // W praktyce można użyć bardziej zaawansowanego cache z możliwością enumeracji kluczy
                // Na razie usuwamy tylko główne klucze
                
                _logger.LogInformation("Cache cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }

        private static PostDto MapToPostDto(Post post)
        {
            return new PostDto
            {
                Id = post.Id,
                Title = post.Title,
                Description = post.Description,
                ContactEmail = post.ContactEmail,
                ContactPhone = post.ContactPhone,
                Status = post.Status,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                ExpiresAt = post.ExpiresAt,
                OrganizationId = post.OrganizationId,
                OrganizationName = post.Organization?.OrganizationName ?? string.Empty,
                Categories = post.PostCategories.Select(pc => new CategorySimpleDto
                {
                    Id = pc.Category.Id,
                    Name = pc.Category.Name
                }).ToList(),
                Tags = post.PostTags.Select(pt => new TagDto
                {
                    Id = pt.Tag.Id,
                    Name = pt.Tag.Name,
                    CategoryId = pt.Tag.CategoryId,
                    CategoryName = pt.Tag.Category?.Name ?? string.Empty
                }).ToList()
            };
        }

        private static PostDto MapToPostDto(Post post, bool includeOrganization, bool includeCategories, bool includeTags)
        {
            var postDto = new PostDto
            {
                Id = post.Id,
                Title = post.Title,
                Description = post.Description,
                ContactEmail = post.ContactEmail,
                ContactPhone = post.ContactPhone,
                Status = post.Status,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                ExpiresAt = post.ExpiresAt,
                OrganizationId = post.OrganizationId,
                OrganizationName = post.Organization?.OrganizationName ?? string.Empty,
                Categories = new List<CategorySimpleDto>(),
                Tags = new List<TagDto>()
            };

            if (includeCategories)
            {
                postDto.Categories = post.PostCategories.Select(pc => new CategorySimpleDto
                {
                    Id = pc.Category.Id,
                    Name = pc.Category.Name
                }).ToList();
            }

            if (includeTags)
            {
                postDto.Tags = post.PostTags.Select(pt => new TagDto
                {
                    Id = pt.Tag.Id,
                    Name = pt.Tag.Name,
                    CategoryId = pt.Tag.CategoryId,
                    CategoryName = pt.Tag.Category?.Name ?? string.Empty
                }).ToList();
            }

            return postDto;
        }

        /// <summary>
        /// Waliduje listę ID i sprawdza czy wszystkie istnieją w bazie danych
        /// </summary>
        /// <typeparam name="T">Typ encji do sprawdzenia</typeparam>
        /// <param name="ids">Lista ID do walidacji</param>
        /// <param name="query">Query do pobrania istniejących ID</param>
        /// <param name="entityName">Nazwa encji dla komunikatu błędu</param>
        /// <returns>Lista istniejących ID</returns>
        /// <exception cref="ArgumentException">Gdy nie wszystkie ID istnieją</exception>
        private async Task<List<int>> ValidateIdsAsync<T>(List<int> ids, IQueryable<T> query, string entityName) where T : class
        {
            if (!ids.Any()) return new List<int>();

            var existingIds = await query
                .Where(e => ids.Contains(EF.Property<int>(e, "Id")))
                .Select(e => EF.Property<int>(e, "Id"))
                .ToListAsync();

            if (existingIds.Count != ids.Count)
            {
                var missingIds = ids.Except(existingIds);
                throw new ArgumentException($"{entityName} not found: {string.Join(", ", missingIds)}");
            }

            return existingIds;
        }
    }
}
