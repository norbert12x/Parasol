using ParasolBackEnd.DTOs;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ParasolBackEnd.Services
{
    public class MatchMakerService : IMatchMakerService
    {
        private readonly ILogger<MatchMakerService> _logger;
        private readonly string _connectionString;

        public MatchMakerService(ILogger<MatchMakerService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("SecondDb") + ";Multiplexing=false;Pooling=false";
        }

        public async Task<List<CategorySimpleDto>> GetCategoriesAsync()
        {
            try
            {
                _logger.LogInformation("Starting GetCategoriesAsync");

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = "SELECT id, name FROM categories ORDER BY name";

                await using var command = new NpgsqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                var categories = new List<CategorySimpleDto>();
                while (await reader.ReadAsync())
                {
                    categories.Add(new CategorySimpleDto
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    });
                }

                _logger.LogInformation("Successfully loaded {Count} categories", categories.Count);
                return categories;
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
                _logger.LogInformation("Starting GetCategoryByIdAsync for category {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                // Pobierz podstawowe info o kategorii
                const string categorySql = "SELECT id, name FROM categories WHERE id = @id";
                await using var categoryCommand = new NpgsqlCommand(categorySql, connection);
                categoryCommand.Parameters.AddWithValue("@id", id);
                
                await using var categoryReader = await categoryCommand.ExecuteReaderAsync();
                if (!await categoryReader.ReadAsync())
                {
                    _logger.LogWarning("Category {Id} not found in database", id);
                    return null;
                }
                
                var categoryId = categoryReader.GetInt32(0);
                var categoryName = categoryReader.GetString(1);
                await categoryReader.CloseAsync();

                // Pobierz tagi używając nowej metody
                var tagNames = await GetTagNamesByCategoryAsync(id);

                var result = new CategoryDto
                {
                    Id = categoryId,
                    Name = categoryName,
                    Tags = tagNames.Select(name => new TagDto
                    {
                        Id = 0, // Nie potrzebujemy ID
                        Name = name,
                        CategoryId = id,
                        CategoryName = categoryName
                    }).ToList()
                };

                _logger.LogInformation("Successfully loaded category {Id} with {TagCount} tags", id, result.Tags.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category by id: {Id}", id);
                return null;
            }
        }
        
        private async Task<List<string>> GetTagNamesByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.LogInformation("Starting GetTagNamesByCategoryAsync for category {CategoryId}", categoryId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = "SELECT name FROM tags WHERE category_id = @categoryId";
                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@categoryId", categoryId);
                
                var tagNames = new List<string>();
                await using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    tagNames.Add(reader.GetString(0));
                }

                _logger.LogInformation("Successfully loaded {Count} tag names for category {CategoryId}", tagNames.Count, categoryId);

                return tagNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag names for category {CategoryId}", categoryId);
                return new List<string>();
            }
        }

        public async Task<List<TagDto>> GetTagsAsync()
        {
            try
            {
                _logger.LogInformation("Starting GetTagsAsync");

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT t.id, t.name, t.category_id, c.name as category_name
                    FROM tags t 
                    JOIN categories c ON t.category_id = c.id
                    ORDER BY t.name";

                await using var command = new NpgsqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                var tags = new List<TagDto>();
                while (await reader.ReadAsync())
                {
                    tags.Add(new TagDto
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        CategoryId = reader.GetInt32(2),
                        CategoryName = reader.GetString(3)
                    });
                }

                _logger.LogInformation("Successfully loaded {Count} tags", tags.Count);
                return tags;
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
                _logger.LogInformation("Starting GetTagsByCategoryAsync for category {CategoryId}", categoryId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT t.id, t.name, t.category_id, c.name as category_name
                    FROM tags t 
                    JOIN categories c ON t.category_id = c.id
                    WHERE t.category_id = @categoryId
                    ORDER BY t.name";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@categoryId", categoryId);
                await using var reader = await command.ExecuteReaderAsync();

                var tags = new List<TagDto>();
                while (await reader.ReadAsync())
                {
                    tags.Add(new TagDto
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        CategoryId = reader.GetInt32(2),
                        CategoryName = reader.GetString(3)
                    });
                }

                _logger.LogInformation("Successfully loaded {Count} tags for category {CategoryId}", tags.Count, categoryId);
                return tags;
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
                _logger.LogInformation("Starting GetTagByIdAsync for tag {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT t.id, t.name, t.category_id, c.name as category_name
                    FROM tags t 
                    JOIN categories c ON t.category_id = c.id
                    WHERE t.id = @id";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@id", id);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var tag = new TagDto
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        CategoryId = reader.GetInt32(2),
                        CategoryName = reader.GetString(3)
                    };

                    _logger.LogInformation("Successfully loaded tag {Id}: {Name}", id, tag.Name);
                    return tag;
                }

                _logger.LogWarning("Tag {Id} not found", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag by id: {Id}", id);
                return null;
            }
        }




        // Metody pomocnicze dla wspólnej logiki SQL
        private async Task<List<CategorySimpleDto>> LoadPostCategoriesAsync(NpgsqlConnection connection, int postId)
        {
            const string categoriesSql = @"
                SELECT c.id, c.name 
                FROM post_categories pc 
                JOIN categories c ON pc.category_id = c.id 
                WHERE pc.post_id = @postId";

            await using var categoriesCommand = new NpgsqlCommand(categoriesSql, connection);
            categoriesCommand.Parameters.AddWithValue("@postId", postId);

            var categories = new List<CategorySimpleDto>();
            await using var categoriesReader = await categoriesCommand.ExecuteReaderAsync();
            while (await categoriesReader.ReadAsync())
            {
                categories.Add(new CategorySimpleDto
                {
                    Id = categoriesReader.GetInt32(0),
                    Name = categoriesReader.GetString(1)
                });
            }
            await categoriesReader.CloseAsync();
            return categories;
        }

        private async Task<List<TagDto>> LoadPostTagsAsync(NpgsqlConnection connection, int postId)
        {
            const string tagsSql = @"
                SELECT t.id, t.name, t.category_id, c.name as category_name
                FROM post_tags pt 
                JOIN tags t ON pt.tag_id = t.id 
                JOIN categories c ON t.category_id = c.id
                WHERE pt.post_id = @postId";

            await using var tagsCommand = new NpgsqlCommand(tagsSql, connection);
            tagsCommand.Parameters.AddWithValue("@postId", postId);

            var tags = new List<TagDto>();
            await using var tagsReader = await tagsCommand.ExecuteReaderAsync();
            while (await tagsReader.ReadAsync())
            {
                tags.Add(new TagDto
                {
                    Id = tagsReader.GetInt32(0),
                    Name = tagsReader.GetString(1),
                    CategoryId = tagsReader.GetInt32(2),
                    CategoryName = tagsReader.GetString(3)
                });
            }
            await tagsReader.CloseAsync();
            return tags;
        }

        private async Task<bool> ValidateCategoriesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<int> categoryIds)
        {
            if (!categoryIds.Any()) return true;

            const string categoryCheckSql = "SELECT COUNT(*) FROM categories WHERE id = ANY(@categoryIds)";
            await using var categoryCheckCommand = new NpgsqlCommand(categoryCheckSql, connection, transaction);
            categoryCheckCommand.Parameters.AddWithValue("@categoryIds", categoryIds.ToArray());

            var categoryCount = (long)await categoryCheckCommand.ExecuteScalarAsync();
            return categoryCount == categoryIds.Count;
        }

        private async Task<bool> ValidateTagsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<int> tagIds)
        {
            if (!tagIds.Any()) return true;

            const string tagCheckSql = "SELECT COUNT(*) FROM tags WHERE id = ANY(@tagIds)";
            await using var tagCheckCommand = new NpgsqlCommand(tagCheckSql, connection, transaction);
            tagCheckCommand.Parameters.AddWithValue("@tagIds", tagIds.ToArray());

            var tagCount = (long)await tagCheckCommand.ExecuteScalarAsync();
            return tagCount == tagIds.Count;
        }


        public async Task<List<PostDto>> GetPostsSummaryAsync(int? categoryId = null, int? tagId = null, string? searchTerm = null, 
            int page = 1, int pageSize = 20)
        {
            try
            {
                _logger.LogInformation("Starting GetPostsSummaryAsync with filters - CategoryId: {CategoryId}, TagId: {TagId}, SearchTerm: {SearchTerm}, Page: {Page}, PageSize: {PageSize}", 
                    categoryId, tagId, searchTerm, page, pageSize);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Buduj zapytanie SQL z filtrami
                var whereConditions = new List<string>();
                var parameters = new List<NpgsqlParameter>();

                if (categoryId.HasValue)
                {
                    whereConditions.Add("EXISTS (SELECT 1 FROM post_categories pc WHERE pc.post_id = p.id AND pc.category_id = @categoryId)");
                    parameters.Add(new NpgsqlParameter("@categoryId", categoryId.Value));
                }

                if (tagId.HasValue)
                {
                    whereConditions.Add("EXISTS (SELECT 1 FROM post_tags pt WHERE pt.post_id = p.id AND pt.tag_id = @tagId)");
                    parameters.Add(new NpgsqlParameter("@tagId", tagId.Value));
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    whereConditions.Add("(p.title ILIKE @searchTerm OR p.description ILIKE @searchTerm)");
                    parameters.Add(new NpgsqlParameter("@searchTerm", $"%{searchTerm}%"));
                }

                var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

                var sql = $@"
                    SELECT p.id, p.title, p.description, p.contact_email, p.contact_phone, 
                           p.status, p.created_at, p.updated_at, p.expires_at, p.organization_id,
                           o.organization_name
                    FROM posts p 
                    JOIN organizations o ON p.organization_id = o.id
                    {whereClause}
                    ORDER BY p.created_at DESC
                    LIMIT @pageSize OFFSET @offset";

                parameters.Add(new NpgsqlParameter("@pageSize", pageSize));
                parameters.Add(new NpgsqlParameter("@offset", (page - 1) * pageSize));

                await using var command = new NpgsqlCommand(sql, connection);
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }

                var posts = new List<PostDto>();
                await using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var postDto = new PostDto
                    {
                        Id = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Description = reader.GetString(2),
                        ContactEmail = reader.GetString(3),
                        ContactPhone = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Status = reader.GetString(5),
                        CreatedAt = DateOnly.FromDateTime(reader.GetDateTime(6)),
                        UpdatedAt = DateOnly.FromDateTime(reader.GetDateTime(7)),
                        ExpiresAt = reader.IsDBNull(8) ? null : DateOnly.FromDateTime(reader.GetDateTime(8)),
                        OrganizationId = reader.GetInt32(9),
                        OrganizationName = reader.GetString(10),
                        Categories = new List<CategorySimpleDto>(),
                        Tags = new List<TagDto>()
                    };
                    posts.Add(postDto);
                }
                await reader.CloseAsync();

                // Dla każdego posta pobierz kategorie i tagi
                foreach (var post in posts)
                {
                    post.Categories = await LoadPostCategoriesAsync(connection, post.Id);
                    post.Tags = await LoadPostTagsAsync(connection, post.Id);
                }

                _logger.LogInformation("Successfully loaded {Count} posts", posts.Count);
                
                return posts;
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
                _logger.LogInformation("Starting GetPostByIdAsync for post {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Pobierz podstawowe info o poście
                const string postSql = @"
                    SELECT p.id, p.title, p.description, p.contact_email, p.contact_phone, 
                           p.status, p.created_at, p.updated_at, p.expires_at, p.organization_id
                    FROM posts p 
                    WHERE p.id = @id";
                
                await using var postCommand = new NpgsqlCommand(postSql, connection);
                postCommand.Parameters.AddWithValue("@id", id);
                
                await using var postReader = await postCommand.ExecuteReaderAsync();
                if (!await postReader.ReadAsync())
                {
                    _logger.LogWarning("Post {Id} not found in database", id);
                    return null;
                }
                
                var postDto = new PostDto
                {
                    Id = postReader.GetInt32(0),
                    Title = postReader.GetString(1),
                    Description = postReader.GetString(2),
                    ContactEmail = postReader.GetString(3),
                    ContactPhone = postReader.IsDBNull(4) ? null : postReader.GetString(4),
                    Status = postReader.GetString(5),
                    CreatedAt = DateOnly.FromDateTime(postReader.GetDateTime(6)),
                    UpdatedAt = DateOnly.FromDateTime(postReader.GetDateTime(7)),
                    ExpiresAt = postReader.IsDBNull(8) ? null : DateOnly.FromDateTime(postReader.GetDateTime(8)),
                    OrganizationId = postReader.GetInt32(9),
                    Categories = new List<CategorySimpleDto>(),
                    Tags = new List<TagDto>()
                };
                
                await postReader.CloseAsync();

                // Pobierz organizację jeśli potrzebna
                if (includeOrganization)
                {
                    const string orgSql = "SELECT organization_name FROM organizations WHERE id = @orgId";
                    await using var orgCommand = new NpgsqlCommand(orgSql, connection);
                    orgCommand.Parameters.AddWithValue("@orgId", postDto.OrganizationId);
                    
                    var orgName = await orgCommand.ExecuteScalarAsync();
                    postDto.OrganizationName = orgName?.ToString() ?? string.Empty;
                }

                // Pobierz kategorie jeśli potrzebne
                if (includeCategories)
                {
                    postDto.Categories = await LoadPostCategoriesAsync(connection, id);
                }

                // Pobierz tagi jeśli potrzebne
                if (includeTags)
                {
                    postDto.Tags = await LoadPostTagsAsync(connection, id);
                }

                _logger.LogInformation("Successfully loaded post {Id} with {CategoryCount} categories and {TagCount} tags", 
                    id, postDto.Categories.Count, postDto.Tags.Count);

                return postDto;
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

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var transaction = await connection.BeginTransactionAsync();
                
                try
                {
                    // Walidacja organizacji
                    const string orgCheckSql = "SELECT id FROM organizations WHERE id = @organizationId";
                    await using var orgCheckCommand = new NpgsqlCommand(orgCheckSql, connection, transaction);
                    orgCheckCommand.Parameters.AddWithValue("@organizationId", createPostDto.OrganizationId);
                    
                    var orgExists = await orgCheckCommand.ExecuteScalarAsync();
                    if (orgExists == null)
                    {
                        throw new ArgumentException($"Organization with ID {createPostDto.OrganizationId} not found");
                    }

                    // Walidacja kategorii
                    var categoryIds = createPostDto.CategoryIds ?? new List<int>();
                    if (!await ValidateCategoriesAsync(connection, transaction, categoryIds))
                    {
                        throw new ArgumentException("Some categories not found");
                    }

                    // Walidacja tagów
                    var tagIds = createPostDto.TagIds ?? new List<int>();
                    if (!await ValidateTagsAsync(connection, transaction, tagIds))
                    {
                        throw new ArgumentException("Some tags not found");
                    }

                    // Wstaw post
                    const string insertPostSql = @"
                        INSERT INTO posts (title, description, contact_email, contact_phone, status, created_at, updated_at, expires_at, organization_id)
                        VALUES (@title, @description, @contactEmail, @contactPhone, @status, @createdAt, @updatedAt, @expiresAt, @organizationId)
                        RETURNING id";
                    
                    await using var insertPostCommand = new NpgsqlCommand(insertPostSql, connection, transaction);
                    insertPostCommand.Parameters.AddWithValue("@title", createPostDto.Title);
                    insertPostCommand.Parameters.AddWithValue("@description", createPostDto.Description);
                    insertPostCommand.Parameters.AddWithValue("@contactEmail", createPostDto.ContactEmail);
                    insertPostCommand.Parameters.AddWithValue("@contactPhone", (object?)createPostDto.ContactPhone ?? DBNull.Value);
                    insertPostCommand.Parameters.AddWithValue("@status", "active");
                    insertPostCommand.Parameters.AddWithValue("@createdAt", DateOnly.FromDateTime(DateTime.UtcNow));
                    insertPostCommand.Parameters.AddWithValue("@updatedAt", DateOnly.FromDateTime(DateTime.UtcNow));
                    insertPostCommand.Parameters.AddWithValue("@expiresAt", (object?)expiresAt ?? DBNull.Value);
                    insertPostCommand.Parameters.AddWithValue("@organizationId", createPostDto.OrganizationId);
                    
                    var postId = (int)await insertPostCommand.ExecuteScalarAsync();

                    // Wstaw kategorie
                    foreach (var categoryId in categoryIds)
                    {
                        const string insertCategorySql = "INSERT INTO post_categories (post_id, category_id) VALUES (@postId, @categoryId)";
                        await using var insertCategoryCommand = new NpgsqlCommand(insertCategorySql, connection, transaction);
                        insertCategoryCommand.Parameters.AddWithValue("@postId", postId);
                        insertCategoryCommand.Parameters.AddWithValue("@categoryId", categoryId);
                        await insertCategoryCommand.ExecuteNonQueryAsync();
                    }

                    // Wstaw tagi
                    foreach (var tagId in tagIds)
                    {
                        const string insertTagSql = "INSERT INTO post_tags (post_id, tag_id) VALUES (@postId, @tagId)";
                        await using var insertTagCommand = new NpgsqlCommand(insertTagSql, connection, transaction);
                        insertTagCommand.Parameters.AddWithValue("@postId", postId);
                        insertTagCommand.Parameters.AddWithValue("@tagId", tagId);
                        await insertTagCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully created post with ID {PostId}, Title: {Title}, Categories: {CategoryCount}, Tags: {TagCount}", 
                        postId, createPostDto.Title, categoryIds.Count, tagIds.Count);

                    // Zwróć utworzony post
                    return await GetPostByIdAsync(postId) ?? new PostDto();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
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
                _logger.LogInformation("Starting UpdatePostAsync for post {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy post istnieje
                    const string checkSql = "SELECT id FROM posts WHERE id = @id";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@id", id);

                    var postExists = await checkCommand.ExecuteScalarAsync();
                    if (postExists == null)
                    {
                        _logger.LogWarning("Post {Id} not found for update", id);
                        return null;
                    }

                    // Walidacja kategorii
                    var categoryIds = updatePostDto.CategoryIds ?? new List<int>();
                    if (!await ValidateCategoriesAsync(connection, transaction, categoryIds))
                    {
                        throw new ArgumentException("Some categories not found");
                    }

                    // Walidacja tagów
                    var tagIds = updatePostDto.TagIds ?? new List<int>();
                    if (!await ValidateTagsAsync(connection, transaction, tagIds))
                    {
                        throw new ArgumentException("Some tags not found");
                    }

                    // Aktualizuj post
                    const string updatePostSql = @"
                        UPDATE posts 
                        SET title = @title, description = @description, contact_email = @contactEmail, 
                            contact_phone = @contactPhone, status = @status, updated_at = @updatedAt, 
                            expires_at = @expiresAt
                        WHERE id = @postId";

                    await using var updatePostCommand = new NpgsqlCommand(updatePostSql, connection, transaction);
                    updatePostCommand.Parameters.AddWithValue("@title", updatePostDto.Title);
                    updatePostCommand.Parameters.AddWithValue("@description", updatePostDto.Description);
                    updatePostCommand.Parameters.AddWithValue("@contactEmail", updatePostDto.ContactEmail);
                    updatePostCommand.Parameters.AddWithValue("@contactPhone", (object?)updatePostDto.ContactPhone ?? DBNull.Value);
                    updatePostCommand.Parameters.AddWithValue("@status", updatePostDto.Status);
                    updatePostCommand.Parameters.AddWithValue("@updatedAt", DateOnly.FromDateTime(DateTime.UtcNow));
                    updatePostCommand.Parameters.AddWithValue("@expiresAt", (object?)updatePostDto.ExpiresAt ?? DBNull.Value);
                    updatePostCommand.Parameters.AddWithValue("@postId", id);

                    await updatePostCommand.ExecuteNonQueryAsync();

                    // Usuń stare kategorie
                    const string deleteCategoriesSql = "DELETE FROM post_categories WHERE post_id = @postId";
                    await using var deleteCategoriesCommand = new NpgsqlCommand(deleteCategoriesSql, connection, transaction);
                    deleteCategoriesCommand.Parameters.AddWithValue("@postId", id);
                    await deleteCategoriesCommand.ExecuteNonQueryAsync();

                    // Dodaj nowe kategorie
                    foreach (var categoryId in categoryIds)
                    {
                        const string insertCategorySql = "INSERT INTO post_categories (post_id, category_id) VALUES (@postId, @categoryId)";
                        await using var insertCategoryCommand = new NpgsqlCommand(insertCategorySql, connection, transaction);
                        insertCategoryCommand.Parameters.AddWithValue("@postId", id);
                        insertCategoryCommand.Parameters.AddWithValue("@categoryId", categoryId);
                        await insertCategoryCommand.ExecuteNonQueryAsync();
                    }

                    // Usuń stare tagi
                    const string deleteTagsSql = "DELETE FROM post_tags WHERE post_id = @postId";
                    await using var deleteTagsCommand = new NpgsqlCommand(deleteTagsSql, connection, transaction);
                    deleteTagsCommand.Parameters.AddWithValue("@postId", id);
                    await deleteTagsCommand.ExecuteNonQueryAsync();

                    // Dodaj nowe tagi
                    foreach (var tagId in tagIds)
                    {
                        const string insertTagSql = "INSERT INTO post_tags (post_id, tag_id) VALUES (@postId, @tagId)";
                        await using var insertTagCommand = new NpgsqlCommand(insertTagSql, connection, transaction);
                        insertTagCommand.Parameters.AddWithValue("@postId", id);
                        insertTagCommand.Parameters.AddWithValue("@tagId", tagId);
                        await insertTagCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully updated post with ID {PostId}, Title: {Title}, Categories: {CategoryCount}, Tags: {TagCount}",
                        id, updatePostDto.Title, categoryIds.Count, tagIds.Count);

                    // Zwróć zaktualizowany post
                    return await GetPostByIdAsync(id);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
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
                _logger.LogInformation("Starting DeletePostAsync for post {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy post istnieje
                    const string checkSql = "SELECT id FROM posts WHERE id = @id";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@id", id);

                    var postExists = await checkCommand.ExecuteScalarAsync();
                    if (postExists == null)
                    {
                        _logger.LogWarning("Post {Id} not found for deletion", id);
                        return false;
                    }

                    // Usuń powiązania z kategoriami
                    const string deleteCategoriesSql = "DELETE FROM post_categories WHERE post_id = @postId";
                    await using var deleteCategoriesCommand = new NpgsqlCommand(deleteCategoriesSql, connection, transaction);
                    deleteCategoriesCommand.Parameters.AddWithValue("@postId", id);
                    await deleteCategoriesCommand.ExecuteNonQueryAsync();

                    // Usuń powiązania z tagami
                    const string deleteTagsSql = "DELETE FROM post_tags WHERE post_id = @postId";
                    await using var deleteTagsCommand = new NpgsqlCommand(deleteTagsSql, connection, transaction);
                    deleteTagsCommand.Parameters.AddWithValue("@postId", id);
                    await deleteTagsCommand.ExecuteNonQueryAsync();

                    // Usuń post
                    const string deletePostSql = "DELETE FROM posts WHERE id = @postId";
                    await using var deletePostCommand = new NpgsqlCommand(deletePostSql, connection, transaction);
                    deletePostCommand.Parameters.AddWithValue("@postId", id);
                    var rowsAffected = await deletePostCommand.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully deleted post {Id}", id);
                    return rowsAffected > 0;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post with ID {PostId}", id);
                return false;
            }
        }

        public async Task<IEnumerable<PostDto>> GetPostsByOrganizationAsync(int organizationId, bool includeCategories = false, bool includeTags = false)
        {
            try
            {
                _logger.LogInformation("Starting GetPostsByOrganizationAsync for organization {OrganizationId}", organizationId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Pobierz podstawowe info o postach organizacji
                const string postsSql = @"
                    SELECT p.id, p.title, p.description, p.contact_email, p.contact_phone, 
                           p.status, p.created_at, p.updated_at, p.expires_at, p.organization_id
                    FROM posts p 
                    WHERE p.organization_id = @organizationId
                    ORDER BY p.created_at DESC";

                await using var postsCommand = new NpgsqlCommand(postsSql, connection);
                postsCommand.Parameters.AddWithValue("@organizationId", organizationId);

                var posts = new List<PostDto>();
                await using var postsReader = await postsCommand.ExecuteReaderAsync();

                while (await postsReader.ReadAsync())
                {
                    var postDto = new PostDto
                    {
                        Id = postsReader.GetInt32(0),
                        Title = postsReader.GetString(1),
                        Description = postsReader.GetString(2),
                        ContactEmail = postsReader.GetString(3),
                        ContactPhone = postsReader.IsDBNull(4) ? null : postsReader.GetString(4),
                        Status = postsReader.GetString(5),
                        CreatedAt = DateOnly.FromDateTime(postsReader.GetDateTime(6)),
                        UpdatedAt = DateOnly.FromDateTime(postsReader.GetDateTime(7)),
                        ExpiresAt = postsReader.IsDBNull(8) ? null : DateOnly.FromDateTime(postsReader.GetDateTime(8)),
                        OrganizationId = postsReader.GetInt32(9),
                        Categories = new List<CategorySimpleDto>(),
                        Tags = new List<TagDto>()
                    };
                    posts.Add(postDto);
                }
                await postsReader.CloseAsync();

                // Dla każdego posta pobierz kategorie i tagi jeśli potrzebne
                foreach (var post in posts)
                {
                    if (includeCategories)
                    {
                        post.Categories = await LoadPostCategoriesAsync(connection, post.Id);
                    }

                    if (includeTags)
                    {
                        post.Tags = await LoadPostTagsAsync(connection, post.Id);
                    }
                }

                _logger.LogInformation("Successfully loaded {Count} posts for organization {OrganizationId}", posts.Count, organizationId);

                return posts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts by organization: {OrganizationId}", organizationId);
                return new List<PostDto>();
            }
        }


    }
}
