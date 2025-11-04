using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ParasolBackEnd.DTOs;
using ParasolBackEnd.Services;
using Npgsql;

namespace ParasolBackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")] // Tylko admini mogą używać tych endpointów
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString; // SecondDb (MatchMaker)
        private readonly string _mapConnectionString; // DefaultConnection (mapa)

        public AdminController(ILogger<AdminController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("SecondDb") + ";Multiplexing=false;Pooling=false";
            _mapConnectionString = configuration.GetConnectionString("DefaultConnection") + ";Multiplexing=false;Pooling=false";
        }

        /// <summary>
        /// Wyszukuje organizację po KRS lub nazwie i zwraca jej szczegóły
        /// </summary>
        /// <param name="q">KRS number lub nazwa organizacji</param>
        /// <returns>Szczegóły organizacji</returns>
        [HttpGet("organizations/search")]
        public async Task<ActionResult<object>> SearchOrganization([FromQuery] string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    return BadRequest("Parametr 'q' jest wymagany");
                }

                _logger.LogInformation("Searching organizations with query: {Query}", q);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Podstawowe dane organizacji
                const string orgSql = @"
                    SELECT o.id, o.email, o.organization_name, o.krs_number, o.role,
                           o.about_text, o.website_url, o.phone, o.contact_email
                    FROM organizations o
                    WHERE o.organization_name ILIKE @search OR o.krs_number ILIKE @search
                    LIMIT 1";

                await using var orgCommand = new NpgsqlCommand(orgSql, connection);
                orgCommand.Parameters.AddWithValue("@search", $"%{q}%");

                await using var orgReader = await orgCommand.ExecuteReaderAsync();

                if (!await orgReader.ReadAsync())
                {
                    _logger.LogWarning("Organization not found for query: {Query}", q);
                    return NotFound("Organizacja nie została znaleziona");
                }

                var organization = new
                {
                    Id = orgReader.GetInt32(0),
                    Email = orgReader.GetString(1),
                    OrganizationName = orgReader.GetString(2),
                    KrsNumber = orgReader.IsDBNull(3) ? null : orgReader.GetString(3),
                    Role = orgReader.GetString(4),
                    AboutText = orgReader.IsDBNull(5) ? null : orgReader.GetString(5),
                    WebsiteUrl = orgReader.IsDBNull(6) ? null : orgReader.GetString(6),
                    Phone = orgReader.IsDBNull(7) ? null : orgReader.GetString(7),
                    ContactEmail = orgReader.IsDBNull(8) ? null : orgReader.GetString(8)
                };

                await orgReader.CloseAsync();

                // Liczba postów organizacji
                const string postsCountSql = @"
                    SELECT COUNT(*) 
                    FROM posts 
                    WHERE organization_id = @id";

                await using var postsCountCommand = new NpgsqlCommand(postsCountSql, connection);
                postsCountCommand.Parameters.AddWithValue("@id", organization.Id);
                var postsCount = await postsCountCommand.ExecuteScalarAsync();

                // Kategorie organizacji
                const string categoriesSql = @"
                    SELECT c.id, c.name 
                    FROM organization_categories oc 
                    JOIN categories c ON oc.category_id = c.id 
                    WHERE oc.organization_id = @id";

                await using var categoriesCommand = new NpgsqlCommand(categoriesSql, connection);
                categoriesCommand.Parameters.AddWithValue("@id", organization.Id);

                var categories = new List<object>();
                await using var categoriesReader = await categoriesCommand.ExecuteReaderAsync();
                while (await categoriesReader.ReadAsync())
                {
                    categories.Add(new
                    {
                        Id = categoriesReader.GetInt32(0),
                        Name = categoriesReader.GetString(1)
                    });
                }
                await categoriesReader.CloseAsync();

                // Tagi organizacji
                const string tagsSql = @"
                    SELECT t.id, t.name 
                    FROM organization_tags ot 
                    JOIN tags t ON ot.tag_id = t.id 
                    WHERE ot.organization_id = @id";

                await using var tagsCommand = new NpgsqlCommand(tagsSql, connection);
                tagsCommand.Parameters.AddWithValue("@id", organization.Id);

                var tags = new List<object>();
                await using var tagsReader = await tagsCommand.ExecuteReaderAsync();
                while (await tagsReader.ReadAsync())
                {
                    tags.Add(new
                    {
                        Id = tagsReader.GetInt32(0),
                        Name = tagsReader.GetString(1)
                    });
                }
                await tagsReader.CloseAsync();

                var result = new
                {
                    organization.Id,
                    organization.Email,
                    organization.OrganizationName,
                    organization.KrsNumber,
                    organization.Role,
                    organization.AboutText,
                    organization.WebsiteUrl,
                    organization.Phone,
                    organization.ContactEmail,
                    PostsCount = postsCount != null ? Convert.ToInt64(postsCount) : 0,
                    Categories = categories,
                    Tags = tags
                };

                _logger.LogInformation("Found organization for query: {Query}", q);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching organizations");
                return StatusCode(500, "Wystąpił błąd podczas wyszukiwania organizacji");
            }
        }

        /// <summary>
        /// Usuwa organizację z bazy danych (wraz z wszystkimi postami i powiązaniami)
        /// </summary>
        /// <param name="id">ID organizacji</param>
        /// <returns>Status operacji</returns>
        [HttpDelete("organizations/{id}")]
        public async Task<ActionResult> DeleteOrganization(int id)
        {
            try
            {
                _logger.LogInformation("Deleting organization ID: {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy organizacja istnieje
                    const string checkSql = "SELECT id FROM organizations WHERE id = @id";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@id", id);

                    var exists = await checkCommand.ExecuteScalarAsync();
                    if (exists == null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound($"Organizacja o ID {id} nie została znaleziona");
                    }

                    // Usuń wszystkie powiązania organizacji z kategoriami i tagami
                    // (powinno być automatycznie przez CASCADE, ale dla pewności)

                    // Usuń posty organizacji (powiązania z kategoriami/tagami usunie CASCADE)
                    const string deletePostsSql = "DELETE FROM posts WHERE organization_id = @id";
                    await using var deletePostsCommand = new NpgsqlCommand(deletePostsSql, connection, transaction);
                    deletePostsCommand.Parameters.AddWithValue("@id", id);
                    await deletePostsCommand.ExecuteNonQueryAsync();

                    // Usuń powiązania organizacji z kategoriami
                    const string deleteCategoriesSql = "DELETE FROM organization_categories WHERE organization_id = @id";
                    await using var deleteCategoriesCommand = new NpgsqlCommand(deleteCategoriesSql, connection, transaction);
                    deleteCategoriesCommand.Parameters.AddWithValue("@id", id);
                    await deleteCategoriesCommand.ExecuteNonQueryAsync();

                    // Usuń powiązania organizacji z tagami
                    const string deleteTagsSql = "DELETE FROM organization_tags WHERE organization_id = @id";
                    await using var deleteTagsCommand = new NpgsqlCommand(deleteTagsSql, connection, transaction);
                    deleteTagsCommand.Parameters.AddWithValue("@id", id);
                    await deleteTagsCommand.ExecuteNonQueryAsync();

                    // Usuń organizację
                    const string deleteOrgSql = "DELETE FROM organizations WHERE id = @id";
                    await using var deleteOrgCommand = new NpgsqlCommand(deleteOrgSql, connection, transaction);
                    deleteOrgCommand.Parameters.AddWithValue("@id", id);
                    await deleteOrgCommand.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Organization ID {Id} has been deleted", id);
                    return Ok(new { message = "Organizacja została usunięta z bazy danych" });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organization ID: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas usuwania organizacji");
            }
        }

        // ============================================================
        // KATEGORIE - CRUD (Create, Update, Delete)
        // ============================================================

        /// <summary>
        /// Dodaje nową kategorię
        /// </summary>
        /// <param name="name">Nazwa kategorii</param>
        /// <returns>Utworzona kategoria</returns>
        [HttpPost("categories")]
        public async Task<ActionResult<object>> CreateCategory([FromBody] string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest("Nazwa kategorii jest wymagana");
                }

                _logger.LogInformation("Creating category: {Name}", name);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    INSERT INTO categories (name)
                    VALUES (@name)
                    RETURNING id, name";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@name", name);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var category = new
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    };

                    _logger.LogInformation("Category created: {Name}", name);
                    return Ok(category);
                }

                return StatusCode(500, "Nie udało się utworzyć kategorii");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") // Duplicate key
            {
                _logger.LogWarning("Category already exists: {Name}", name);
                return BadRequest($"Kategoria '{name}' już istnieje");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category: {Name}", name);
                return StatusCode(500, "Wystąpił błąd podczas tworzenia kategorii");
            }
        }

        /// <summary>
        /// Edytuje nazwę kategorii po nazwie
        /// </summary>
        /// <param name="oldName">Stara nazwa kategorii</param>
        /// <param name="newName">Nowa nazwa</param>
        /// <returns>Zaktualizowana kategoria</returns>
        [HttpPut("categories")]
        public async Task<ActionResult<object>> UpdateCategory([FromBody] UpdateCategoryDto updateDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(updateDto.OldName) || string.IsNullOrWhiteSpace(updateDto.NewName))
                {
                    return BadRequest("Stara i nowa nazwa kategorii są wymagane");
                }

                _logger.LogInformation("Updating category from '{OldName}' to '{NewName}'", updateDto.OldName, updateDto.NewName);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy stara nazwa istnieje
                const string checkSql = "SELECT id FROM categories WHERE name = @oldName";
                await using var checkCommand = new NpgsqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@oldName", updateDto.OldName);

                var exists = await checkCommand.ExecuteScalarAsync();
                if (exists == null)
                {
                    return NotFound($"Kategoria '{updateDto.OldName}' nie została znaleziona");
                }

                // Zaktualizuj po nazwie
                const string updateSql = @"
                    UPDATE categories 
                    SET name = @newName 
                    WHERE name = @oldName
                    RETURNING id, name";

                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                updateCommand.Parameters.AddWithValue("@oldName", updateDto.OldName);
                updateCommand.Parameters.AddWithValue("@newName", updateDto.NewName);

                await using var reader = await updateCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var category = new
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    };

                    _logger.LogInformation("Category updated: OldName '{OldName}' -> NewName '{NewName}'", updateDto.OldName, updateDto.NewName);
                    return Ok(category);
                }

                return StatusCode(500, "Nie udało się zaktualizować kategorii");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") // Duplicate key
            {
                _logger.LogWarning("Category name already exists: {Name}", updateDto.NewName);
                return BadRequest($"Kategoria '{updateDto.NewName}' już istnieje");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category: {OldName}", updateDto.OldName);
                return StatusCode(500, "Wystąpił błąd podczas aktualizacji kategorii");
            }
        }

        /// <summary>
        /// Usuwa kategorię po nazwie (wszystkie powiązane tagi również zostaną usunięte przez CASCADE)
        /// </summary>
        /// <param name="name">Nazwa kategorii</param>
        /// <returns>Status operacji</returns>
        [HttpDelete("categories")]
        public async Task<ActionResult> DeleteCategory([FromQuery] string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest("Nazwa kategorii jest wymagana");
                }

                _logger.LogInformation("Deleting category: {Name}", name);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy istnieje
                const string checkSql = "SELECT id FROM categories WHERE name = @name";
                await using var checkCommand = new NpgsqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@name", name);

                var exists = await checkCommand.ExecuteScalarAsync();
                if (exists == null)
                {
                    return NotFound($"Kategoria '{name}' nie została znaleziona");
                }

                // Usuń po nazwie (CASCADE automatycznie usunie tagi)
                const string deleteSql = "DELETE FROM categories WHERE name = @name";
                await using var deleteCommand = new NpgsqlCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@name", name);
                await deleteCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("Category '{Name}' has been deleted", name);
                return Ok(new { message = $"Kategoria '{name}' została usunięta (wszystkie powiązane tagi również)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category: {Name}", name);
                return StatusCode(500, "Wystąpił błąd podczas usuwania kategorii");
            }
        }

        // ============================================================
        // TAGI - CRUD (Create, Update, Delete)
        // ============================================================

        /// <summary>
        /// Dodaje nowy tag
        /// </summary>
        /// <param name="createTagDto">Dane tagu (name, categoryId)</param>
        /// <returns>Utworzony tag</returns>
        [HttpPost("tags")]
        public async Task<ActionResult<object>> CreateTag([FromBody] CreateTagDto createTagDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(createTagDto.Name) || createTagDto.CategoryId <= 0)
                {
                    return BadRequest("Nazwa tagu i ID kategorii są wymagane");
                }

                _logger.LogInformation("Creating tag: {Name}, CategoryId: {CategoryId}", createTagDto.Name, createTagDto.CategoryId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy kategoria istnieje
                const string checkCatSql = "SELECT id FROM categories WHERE id = @categoryId";
                await using var checkCatCommand = new NpgsqlCommand(checkCatSql, connection);
                checkCatCommand.Parameters.AddWithValue("@categoryId", createTagDto.CategoryId);

                var categoryExists = await checkCatCommand.ExecuteScalarAsync();
                if (categoryExists == null)
                {
                    return BadRequest($"Kategoria o ID {createTagDto.CategoryId} nie istnieje");
                }

                // Wstaw tag
                const string sql = @"
                    INSERT INTO tags (name, category_id)
                    VALUES (@name, @categoryId)
                    RETURNING id, name, category_id";

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@name", createTagDto.Name);
                command.Parameters.AddWithValue("@categoryId", createTagDto.CategoryId);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var tag = new
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        CategoryId = reader.GetInt32(2)
                    };

                    _logger.LogInformation("Tag created: {Name}", createTagDto.Name);
                    return Ok(tag);
                }

                return StatusCode(500, "Nie udało się utworzyć tagu");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") // Duplicate key
            {
                _logger.LogWarning("Tag already exists: {Name}", createTagDto.Name);
                return BadRequest($"Tag '{createTagDto.Name}' już istnieje");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tag: {Name}", createTagDto.Name);
                return StatusCode(500, "Wystąpił błąd podczas tworzenia tagu");
            }
        }

        /// <summary>
        /// Edytuje tag po kategorii i nazwie
        /// </summary>
        /// <param name="updateTagDto">Dane tagu (categoryId, tagName, newName, newCategoryId)</param>
        /// <returns>Zaktualizowany tag</returns>
        [HttpPut("tags")]
        public async Task<ActionResult<object>> UpdateTag([FromBody] UpdateTagByNameDto updateTagDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(updateTagDto.TagName) || string.IsNullOrWhiteSpace(updateTagDto.NewName) || 
                    updateTagDto.CategoryId <= 0 || updateTagDto.NewCategoryId <= 0)
                {
                    return BadRequest("Nazwa tagu, nowa nazwa, ID kategorii i nowe ID kategorii są wymagane");
                }

                _logger.LogInformation("Updating tag: '{TagName}' in category {CategoryId} -> '{NewName}' in category {NewCategoryId}", 
                    updateTagDto.TagName, updateTagDto.CategoryId, updateTagDto.NewName, updateTagDto.NewCategoryId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy tag istnieje (w danej kategorii)
                const string checkSql = "SELECT id FROM tags WHERE name = @tagName AND category_id = @categoryId";
                await using var checkCommand = new NpgsqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@tagName", updateTagDto.TagName);
                checkCommand.Parameters.AddWithValue("@categoryId", updateTagDto.CategoryId);

                var tagExists = await checkCommand.ExecuteScalarAsync();
                if (tagExists == null)
                {
                    return NotFound($"Tag '{updateTagDto.TagName}' w kategorii o ID {updateTagDto.CategoryId} nie został znaleziony");
                }

                // Sprawdź czy nowa kategoria istnieje
                const string checkNewCatSql = "SELECT id FROM categories WHERE id = @newCategoryId";
                await using var checkNewCatCommand = new NpgsqlCommand(checkNewCatSql, connection);
                checkNewCatCommand.Parameters.AddWithValue("@newCategoryId", updateTagDto.NewCategoryId);

                var newCategoryExists = await checkNewCatCommand.ExecuteScalarAsync();
                if (newCategoryExists == null)
                {
                    return BadRequest($"Nowa kategoria o ID {updateTagDto.NewCategoryId} nie istnieje");
                }

                // Zaktualizuj po nazwie i kategorii
                const string updateSql = @"
                    UPDATE tags 
                    SET name = @newName, category_id = @newCategoryId 
                    WHERE name = @tagName AND category_id = @categoryId
                    RETURNING id, name, category_id";

                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                updateCommand.Parameters.AddWithValue("@tagName", updateTagDto.TagName);
                updateCommand.Parameters.AddWithValue("@categoryId", updateTagDto.CategoryId);
                updateCommand.Parameters.AddWithValue("@newName", updateTagDto.NewName);
                updateCommand.Parameters.AddWithValue("@newCategoryId", updateTagDto.NewCategoryId);

                await using var reader = await updateCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var tag = new
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        CategoryId = reader.GetInt32(2)
                    };

                    _logger.LogInformation("Tag '{TagName}' updated", updateTagDto.TagName);
                    return Ok(tag);
                }

                return StatusCode(500, "Nie udało się zaktualizować tagu");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") // Duplicate key
            {
                _logger.LogWarning("Tag name already exists: {Name}", updateTagDto.NewName);
                return BadRequest($"Tag '{updateTagDto.NewName}' już istnieje");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tag: {TagName}", updateTagDto.TagName);
                return StatusCode(500, "Wystąpił błąd podczas aktualizacji tagu");
            }
        }

        /// <summary>
        /// Usuwa tag po nazwie i kategorii
        /// </summary>
        /// <param name="categoryId">ID kategorii</param>
        /// <param name="tagName">Nazwa tagu</param>
        /// <returns>Status operacji</returns>
        [HttpDelete("tags")]
        public async Task<ActionResult> DeleteTag([FromQuery] int categoryId, [FromQuery] string tagName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tagName) || categoryId <= 0)
                {
                    return BadRequest("ID kategorii i nazwa tagu są wymagane");
                }

                _logger.LogInformation("Deleting tag: '{TagName}' in category: {CategoryId}", tagName, categoryId);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy tag istnieje (w danej kategorii)
                const string checkSql = "SELECT id FROM tags WHERE name = @tagName AND category_id = @categoryId";
                await using var checkCommand = new NpgsqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@tagName", tagName);
                checkCommand.Parameters.AddWithValue("@categoryId", categoryId);

                var exists = await checkCommand.ExecuteScalarAsync();
                if (exists == null)
                {
                    return NotFound($"Tag '{tagName}' w kategorii o ID {categoryId} nie został znaleziony");
                }

                // Usuń po nazwie i kategorii (CASCADE automatycznie usunie powiązania z postami i organizacjami)
                const string deleteSql = "DELETE FROM tags WHERE name = @tagName AND category_id = @categoryId";
                await using var deleteCommand = new NpgsqlCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@tagName", tagName);
                deleteCommand.Parameters.AddWithValue("@categoryId", categoryId);
                await deleteCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("Tag '{TagName}' has been deleted", tagName);
                return Ok(new { message = $"Tag '{tagName}' został usunięty" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tag: {TagName}", tagName);
                return StatusCode(500, "Wystąpił błąd podczas usuwania tagu");
            }
        }

        // ============================================================
        // STATYSTYKI
        // ============================================================

        /// <summary>
        /// Pobiera podstawowe statystyki systemu
        /// </summary>
        /// <returns>Statystyki systemu</returns>
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            try
            {
                _logger.LogInformation("Getting system statistics");

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Liczba organizacji
                const string orgsCountSql = "SELECT COUNT(*) FROM organizations";
                await using var orgsCountCommand = new NpgsqlCommand(orgsCountSql, connection);
                var orgsCount = Convert.ToInt32(await orgsCountCommand.ExecuteScalarAsync());

                // Liczba postów aktywnych
                const string activePostsSql = "SELECT COUNT(*) FROM posts WHERE status = 'active'";
                await using var activePostsCommand = new NpgsqlCommand(activePostsSql, connection);
                var activePostsCount = Convert.ToInt32(await activePostsCommand.ExecuteScalarAsync());

                // Liczba postów nieaktywnych
                const string inactivePostsSql = "SELECT COUNT(*) FROM posts WHERE status = 'inactive'";
                await using var inactivePostsCommand = new NpgsqlCommand(inactivePostsSql, connection);
                var inactivePostsCount = Convert.ToInt32(await inactivePostsCommand.ExecuteScalarAsync());

                // Ostatnie 5 organizacji (bez sortowania po dacie - bo nie ma kolumny created_at)
                const string recentOrgsSql = @"
                    SELECT id, organization_name, email
                    FROM organizations 
                    ORDER BY id DESC 
                    LIMIT 5";
                
                var recentOrganizations = new List<object>();
                await using var recentOrgsCommand = new NpgsqlCommand(recentOrgsSql, connection);
                await using var orgsReader = await recentOrgsCommand.ExecuteReaderAsync();
                
                while (await orgsReader.ReadAsync())
                {
                    recentOrganizations.Add(new
                    {
                        Id = orgsReader.GetInt32(0),
                        OrganizationName = orgsReader.GetString(1),
                        Email = orgsReader.GetString(2)
                    });
                }

                // Najnowszy post
                object? latestPost = null;
                
                try
                {
                    const string latestPostSql = @"
                        SELECT p.id, p.title, p.contact_email, p.created_at, o.organization_name
                        FROM posts p
                        JOIN organizations o ON p.organization_id = o.id
                        WHERE p.status = 'active'
                        ORDER BY p.id DESC
                        LIMIT 1";
                    
                    await using var latestPostCommand = new NpgsqlCommand(latestPostSql, connection);
                    await using var latestPostReader = await latestPostCommand.ExecuteReaderAsync();
                    
                    if (await latestPostReader.ReadAsync())
                    {
                        var createdAt = latestPostReader.IsDBNull(3) 
                            ? (DateTime?)null 
                            : latestPostReader.GetDateTime(3);
                        
                        latestPost = new
                        {
                            Id = latestPostReader.GetInt32(0),
                            Title = latestPostReader.GetString(1),
                            ContactEmail = latestPostReader.GetString(2),
                            CreatedAt = createdAt,
                            OrganizationName = latestPostReader.GetString(4)
                        };
                    }
                }
                catch
                {
                    // Jeśli baza jest pusta lub nie ma postów, latestPost pozostanie null
                    latestPost = null;
                }

                return Ok(new
                {
                    Organizations = new
                    {
                        Total = orgsCount,
                        Recent = recentOrganizations
                    },
                    Posts = new
                    {
                        Active = activePostsCount,
                        Inactive = inactivePostsCount,
                        Total = activePostsCount + inactivePostsCount
                    },
                    LatestPost = latestPost
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system statistics");
                return StatusCode(500, "Wystąpił błąd podczas pobierania statystyk");
            }
        }

        // ============================================================
        // ZARZĄDZANIE POSTAMI
        // ============================================================

        /// <summary>
        /// Pobiera wszystkie posty organizacji (aktywne i nieaktywne) po KRS lub nazwie
        /// </summary>
        /// <param name="organizationSearch">KRS number lub nazwa organizacji</param>
        /// <returns>Lista wszystkich postów organizacji</returns>
        [HttpGet("posts")]
        public async Task<ActionResult<List<object>>> GetOrganizationPosts([FromQuery] string organizationSearch)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(organizationSearch))
                {
                    return BadRequest("Parametr 'organizationSearch' jest wymagany (KRS lub nazwa organizacji)");
                }

                _logger.LogInformation("Getting all posts for organization: {Search}", organizationSearch);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Najpierw znajdź organizację
                const string findOrgSql = @"
                    SELECT id, organization_name FROM organizations 
                    WHERE organization_name ILIKE @search OR krs_number ILIKE @search
                    LIMIT 1";

                await using var findOrgCommand = new NpgsqlCommand(findOrgSql, connection);
                findOrgCommand.Parameters.AddWithValue("@search", $"%{organizationSearch}%");

                await using var orgReader = await findOrgCommand.ExecuteReaderAsync();
                if (!await orgReader.ReadAsync())
                {
                    return NotFound($"Organizacja nie została znaleziona dla: {organizationSearch}");
                }

                int orgId = orgReader.GetInt32(0);
                string orgName = orgReader.GetString(1);
                await orgReader.CloseAsync();

                // Pobierz WSZYSTKIE posty organizacji (aktywne i nieaktywne)
                const string postsSql = @"
                    SELECT id, title, description, contact_email, contact_phone, 
                           status, created_at, updated_at, expires_at
                    FROM posts
                    WHERE organization_id = @organizationId
                    ORDER BY created_at DESC";

                await using var postsCommand = new NpgsqlCommand(postsSql, connection);
                postsCommand.Parameters.AddWithValue("@organizationId", orgId);

                // Najpierw pobierz wszystkie dane postów do listy
                var postsData = new List<(int Id, string Title, string Description, string ContactEmail, string? ContactPhone, string Status, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ExpiresAt)>();
                await using (var reader = await postsCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        postsData.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4),
                            reader.GetString(5),
                            reader.GetDateTime(6),
                            reader.GetDateTime(7),
                            reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8)
                        ));
                    }
                }

                // Teraz dla każdego posta pobierz kategorie i tagi
                var posts = new List<object>();
                foreach (var postData in postsData)
                {
                    int postId = postData.Id;

                    // Pobierz kategorie dla posta
                    const string categoriesSql = @"
                        SELECT c.id, c.name 
                        FROM post_categories pc
                        JOIN categories c ON pc.category_id = c.id
                        WHERE pc.post_id = @postId";

                    var categories = new List<object>();
                    await using (var categoriesCommand = new NpgsqlCommand(categoriesSql, connection))
                    {
                        categoriesCommand.Parameters.AddWithValue("@postId", postId);
                        await using var categoriesReader = await categoriesCommand.ExecuteReaderAsync();
                        while (await categoriesReader.ReadAsync())
                        {
                            categories.Add(new
                            {
                                Id = categoriesReader.GetInt32(0),
                                Name = categoriesReader.GetString(1)
                            });
                        }
                    }

                    // Pobierz tagi dla posta
                    const string tagsSql = @"
                        SELECT t.id, t.name 
                        FROM post_tags pt
                        JOIN tags t ON pt.tag_id = t.id
                        WHERE pt.post_id = @postId";

                    var tags = new List<object>();
                    await using (var tagsCommand = new NpgsqlCommand(tagsSql, connection))
                    {
                        tagsCommand.Parameters.AddWithValue("@postId", postId);
                        await using var tagsReader = await tagsCommand.ExecuteReaderAsync();
                        while (await tagsReader.ReadAsync())
                        {
                            tags.Add(new
                            {
                                Id = tagsReader.GetInt32(0),
                                Name = tagsReader.GetString(1)
                            });
                        }
                    }

                    posts.Add(new
                    {
                        Id = postData.Id,
                        Title = postData.Title,
                        Description = postData.Description,
                        ContactEmail = postData.ContactEmail,
                        ContactPhone = postData.ContactPhone,
                        Status = postData.Status,
                        CreatedAt = postData.CreatedAt,
                        UpdatedAt = postData.UpdatedAt,
                        ExpiresAt = postData.ExpiresAt,
                        OrganizationId = orgId,
                        OrganizationName = orgName,
                        Categories = categories,
                        Tags = tags
                    });
                }

                _logger.LogInformation("Found {Count} posts for organization: {OrgName}", posts.Count, orgName);
                return Ok(new { organizationId = orgId, organizationName = orgName, posts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization posts for search: {Search}", organizationSearch);
                return StatusCode(500, "Wystąpił błąd podczas pobierania postów");
            }
        }

        /// <summary>
        /// Aktywuje post (ustawia status = 'active')
        /// </summary>
        /// <param name="id">ID posta</param>
        /// <returns>Status operacji</returns>
        [HttpPut("posts/{id}/activate")]
        public async Task<ActionResult> ActivatePost(int id)
        {
            try
            {
                _logger.LogInformation("Activating post ID: {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy post istnieje
                const string checkSql = "SELECT id FROM posts WHERE id = @id";
                await using var checkCommand = new NpgsqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@id", id);

                var exists = await checkCommand.ExecuteScalarAsync();
                if (exists == null)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }

                // Aktywuj
                const string updateSql = "UPDATE posts SET status = 'active', updated_at = CURRENT_DATE WHERE id = @id";
                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                updateCommand.Parameters.AddWithValue("@id", id);
                await updateCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("Post ID {Id} has been activated", id);
                return Ok(new { message = "Post został aktywowany" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating post ID: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas aktywacji posta");
            }
        }

        /// <summary>
        /// Deaktywuje post (ustawia status = 'inactive')
        /// </summary>
        /// <param name="id">ID posta</param>
        /// <returns>Status operacji</returns>
        [HttpPut("posts/{id}/deactivate")]
        public async Task<ActionResult> DeactivatePost(int id)
        {
            try
            {
                _logger.LogInformation("Deactivating post ID: {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy post istnieje
                const string checkSql = "SELECT id FROM posts WHERE id = @id";
                await using var checkCommand = new NpgsqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@id", id);

                var exists = await checkCommand.ExecuteScalarAsync();
                if (exists == null)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }

                // Deaktywuj
                const string updateSql = "UPDATE posts SET status = 'inactive', updated_at = CURRENT_DATE WHERE id = @id";
                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                updateCommand.Parameters.AddWithValue("@id", id);
                await updateCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("Post ID {Id} has been deactivated", id);
                return Ok(new { message = "Post został deaktywowany" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating post ID: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas deaktywacji posta");
            }
        }

        /// <summary>
        /// Usuwa post (wraz z powiązaniami kategorii i tagów)
        /// </summary>
        /// <param name="id">ID posta</param>
        /// <returns>Status operacji</returns>
        [HttpDelete("posts/{id}")]
        public async Task<ActionResult> DeletePost(int id)
        {
            try
            {
                _logger.LogInformation("Deleting post ID: {Id}", id);

                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy post istnieje
                    const string checkSql = "SELECT id FROM posts WHERE id = @id";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@id", id);

                    var exists = await checkCommand.ExecuteScalarAsync();
                    if (exists == null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound($"Post o ID {id} nie został znaleziony");
                    }

                    // Usuń powiązania z kategoriami (CASCADE powinno zrobić to automatycznie, ale dla pewności)
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
                    await deletePostCommand.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Post ID {Id} has been deleted", id);
                    return Ok(new { message = "Post został usunięty" });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post ID: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas usuwania posta");
            }
        }

        // ============================================================
        // ZARZĄDZANIE DANYMI MAPOWYMI (AppDbContext)
        // ============================================================

        /// <summary>
        /// Wyszukuje organizację na mapie po KRS i zwraca jej adresy i koordynaty
        /// </summary>
        /// <param name="krs">Numer KRS</param>
        /// <returns>Szczegóły organizacji z adresami i koordynatami</returns>
        [HttpGet("map/organizations/search")]
        public async Task<ActionResult<object>> SearchMapOrganization([FromQuery] string krs)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(krs))
                {
                    return BadRequest("Parametr 'krs' jest wymagany");
                }

                _logger.LogInformation("Searching map organization with KRS: {Krs}", krs);

                await using var connection = new NpgsqlConnection(_mapConnectionString);
                await connection.OpenAsync();

                // Znajdź organizację
                string nazwa = string.Empty;
                await using (var orgCommand = new NpgsqlCommand("SELECT numerkrs, nazwa FROM organizacja WHERE numerkrs = @krs", connection))
                {
                    orgCommand.Parameters.AddWithValue("@krs", krs);
                    await using var orgReader = await orgCommand.ExecuteReaderAsync();
                    
                    if (!await orgReader.ReadAsync())
                    {
                        return NotFound($"Organizacja o numerze KRS '{krs}' nie została znaleziona");
                    }

                    nazwa = orgReader.GetString(1);
                }

                // Pobierz adresy (po zamknięciu orgReader)
                const string adresSql = @"
                    SELECT id, numerkrs, ulica, nrdomu, nrlokalu, miejscowosc, kodpocztowy, 
                           poczta, gmina, powiat, wojewodztwo, kraj
                    FROM adres 
                    WHERE numerkrs = @krs";
                
                var adresy = new List<object>();
                await using (var adresCommand = new NpgsqlCommand(adresSql, connection))
                {
                    adresCommand.Parameters.AddWithValue("@krs", krs);
                    await using var adresReader = await adresCommand.ExecuteReaderAsync();
                    
                    while (await adresReader.ReadAsync())
                    {
                        adresy.Add(new
                        {
                            Id = adresReader.GetInt32(0),
                            NumerKrs = adresReader.GetString(1),
                            Ulica = adresReader.IsDBNull(2) ? null : adresReader.GetString(2),
                            NrDomu = adresReader.IsDBNull(3) ? null : adresReader.GetString(3),
                            NrLokalu = adresReader.IsDBNull(4) ? null : adresReader.GetString(4),
                            Miejscowosc = adresReader.IsDBNull(5) ? null : adresReader.GetString(5),
                            KodPocztowy = adresReader.IsDBNull(6) ? null : adresReader.GetString(6),
                            Poczta = adresReader.IsDBNull(7) ? null : adresReader.GetString(7),
                            Gmina = adresReader.IsDBNull(8) ? null : adresReader.GetString(8),
                            Powiat = adresReader.IsDBNull(9) ? null : adresReader.GetString(9),
                            Wojewodztwo = adresReader.IsDBNull(10) ? null : adresReader.GetString(10),
                            Kraj = adresReader.IsDBNull(11) ? null : adresReader.GetString(11)
                        });
                    }
                }

                // Pobierz koordynaty (po zamknięciu adresReader)
                const string koordSql = @"
                    SELECT id, numerkrs, latitude, longitude 
                    FROM koordynaty 
                    WHERE numerkrs = @krs";
                
                var koordynaty = new List<object>();
                await using (var koordCommand = new NpgsqlCommand(koordSql, connection))
                {
                    koordCommand.Parameters.AddWithValue("@krs", krs);
                    await using var koordReader = await koordCommand.ExecuteReaderAsync();
                    
                    while (await koordReader.ReadAsync())
                    {
                        koordynaty.Add(new
                        {
                            Id = koordReader.GetInt32(0),
                            NumerKrs = koordReader.GetString(1),
                            Latitude = koordReader.GetDouble(2),
                            Longitude = koordReader.GetDouble(3)
                        });
                    }
                }

                return Ok(new
                {
                    NumerKrs = krs,
                    Nazwa = nazwa,
                    Adresy = adresy,
                    Koordynaty = koordynaty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching map organization with KRS: {Krs}", krs);
                return StatusCode(500, "Wystąpił błąd podczas wyszukiwania organizacji");
            }
        }

        /// <summary>
        /// Aktualizuje koordynaty organizacji
        /// </summary>
        /// <param name="krs">Numer KRS organizacji</param>
        /// <param name="dto">Nowe współrzędne</param>
        /// <returns>Zaktualizowane koordynaty</returns>
        [HttpPut("map/koordynaty")]
        public async Task<ActionResult<object>> UpdateKoordynaty([FromQuery] string krs, [FromBody] UpdateKoordynatyDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(krs))
                {
                    return BadRequest("Parametr 'krs' jest wymagany");
                }

                if (dto.Latitude < -90 || dto.Latitude > 90 || dto.Longitude < -180 || dto.Longitude > 180)
                {
                    return BadRequest("Nieprawidłowe wartości koordynatów (Latitude: -90 do 90, Longitude: -180 do 180)");
                }

                _logger.LogInformation("Updating koordynaty for KRS: {Krs}", krs);

                await using var connection = new NpgsqlConnection(_mapConnectionString);
                await connection.OpenAsync();

                // Sprawdź ile jest koordynatów dla tego KRS
                const string countSql = "SELECT COUNT(*) FROM koordynaty WHERE numerkrs = @krs";
                await using var countCommand = new NpgsqlCommand(countSql, connection);
                countCommand.Parameters.AddWithValue("@krs", krs);
                var koordCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                
                if (koordCount == 0)
                {
                    return NotFound($"Nie znaleziono koordynatów dla numeru KRS '{krs}'");
                }

                if (koordCount > 1)
                {
                    return BadRequest($"Organizacja ma {koordCount} zestawów koordynatów. Podaż konkretny ID koordynatów do aktualizacji.");
                }

                // Zaktualizuj koordynaty (jest tylko jeden)
                const string updateSql = @"
                    UPDATE koordynaty 
                    SET latitude = @lat, longitude = @lon 
                    WHERE numerkrs = @krs 
                    RETURNING id, numerkrs, latitude, longitude";
                
                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                updateCommand.Parameters.AddWithValue("@krs", krs);
                updateCommand.Parameters.AddWithValue("@lat", dto.Latitude);
                updateCommand.Parameters.AddWithValue("@lon", dto.Longitude);
                
                await using var reader = await updateCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var koordynaty = new
                    {
                        Id = reader.GetInt32(0),
                        NumerKrs = reader.GetString(1),
                        Latitude = reader.GetDouble(2),
                        Longitude = reader.GetDouble(3)
                    };

                    _logger.LogInformation("Koordynaty for KRS {Krs} have been updated", krs);
                    return Ok(koordynaty);
                }

                return StatusCode(500, "Nie udało się zaktualizować koordynatów");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating koordynaty for KRS: {Krs}", krs);
                return StatusCode(500, "Wystąpił błąd podczas aktualizacji koordynatów");
            }
        }

        /// <summary>
        /// Aktualizuje adres organizacji
        /// </summary>
        /// <param name="krs">Numer KRS organizacji</param>
        /// <param name="dto">Nowe dane adresowe</param>
        /// <returns>Zaktualizowany adres</returns>
        [HttpPut("map/adres")]
        public async Task<ActionResult<object>> UpdateAdres([FromQuery] string krs, [FromBody] UpdateAdresDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(krs))
                {
                    return BadRequest("Parametr 'krs' jest wymagany");
                }

                _logger.LogInformation("Updating adres for KRS: {Krs}", krs);

                await using var connection = new NpgsqlConnection(_mapConnectionString);
                await connection.OpenAsync();

                // Sprawdź ile jest adresów dla tego KRS
                const string countSql = "SELECT COUNT(*) FROM adres WHERE numerkrs = @krs";
                await using var countCommand = new NpgsqlCommand(countSql, connection);
                countCommand.Parameters.AddWithValue("@krs", krs);
                var adresCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                
                if (adresCount == 0)
                {
                    return NotFound($"Nie znaleziono adresów dla numeru KRS '{krs}'");
                }

                if (adresCount > 1)
                {
                    return BadRequest($"Organizacja ma {adresCount} adresów. Podaj konkretny ID adresu do aktualizacji.");
                }

                // Zbuduj dynamicznie query UPDATE
                var updateFields = new List<string>();
                var parameters = new List<NpgsqlParameter>();

                if (dto.Ulica != null)
                {
                    updateFields.Add("ulica = @ulica");
                    parameters.Add(new NpgsqlParameter("@ulica", dto.Ulica));
                }
                if (dto.NrDomu != null)
                {
                    updateFields.Add("nrdomu = @nrdomu");
                    parameters.Add(new NpgsqlParameter("@nrdomu", dto.NrDomu));
                }
                if (dto.NrLokalu != null)
                {
                    updateFields.Add("nrlokalu = @nrlokalu");
                    parameters.Add(new NpgsqlParameter("@nrlokalu", dto.NrLokalu));
                }
                if (dto.Miejscowosc != null)
                {
                    updateFields.Add("miejscowosc = @miejscowosc");
                    parameters.Add(new NpgsqlParameter("@miejscowosc", dto.Miejscowosc));
                }
                if (dto.KodPocztowy != null)
                {
                    updateFields.Add("kodpocztowy = @kodpocztowy");
                    parameters.Add(new NpgsqlParameter("@kodpocztowy", dto.KodPocztowy));
                }
                if (dto.Poczta != null)
                {
                    updateFields.Add("poczta = @poczta");
                    parameters.Add(new NpgsqlParameter("@poczta", dto.Poczta));
                }
                if (dto.Gmina != null)
                {
                    updateFields.Add("gmina = @gmina");
                    parameters.Add(new NpgsqlParameter("@gmina", dto.Gmina));
                }
                if (dto.Powiat != null)
                {
                    updateFields.Add("powiat = @powiat");
                    parameters.Add(new NpgsqlParameter("@powiat", dto.Powiat));
                }
                if (dto.Wojewodztwo != null)
                {
                    updateFields.Add("wojewodztwo = @wojewodztwo");
                    parameters.Add(new NpgsqlParameter("@wojewodztwo", dto.Wojewodztwo));
                }
                if (dto.Kraj != null)
                {
                    updateFields.Add("kraj = @kraj");
                    parameters.Add(new NpgsqlParameter("@kraj", dto.Kraj));
                }

                if (!updateFields.Any())
                {
                    return BadRequest("Nie podano żadnych danych do aktualizacji");
                }

                var updateSql = $"UPDATE adres SET {string.Join(", ", updateFields)} WHERE numerkrs = @krs";
                
                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                foreach (var param in parameters)
                {
                    updateCommand.Parameters.Add(param);
                }
                updateCommand.Parameters.AddWithValue("@krs", krs);
                await updateCommand.ExecuteNonQueryAsync();

                // Pobierz zaktualizowane dane
                const string selectSql = @"
                    SELECT id, numerkrs, ulica, nrdomu, nrlokalu, miejscowosc, kodpocztowy, 
                           poczta, gmina, powiat, wojewodztwo, kraj
                    FROM adres WHERE numerkrs = @krs";
                
                await using var selectCommand = new NpgsqlCommand(selectSql, connection);
                selectCommand.Parameters.AddWithValue("@krs", krs);
                await using var reader = await selectCommand.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var adres = new
                    {
                        Id = reader.GetInt32(0),
                        NumerKrs = reader.GetString(1),
                        Ulica = reader.IsDBNull(2) ? null : reader.GetString(2),
                        NrDomu = reader.IsDBNull(3) ? null : reader.GetString(3),
                        NrLokalu = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Miejscowosc = reader.IsDBNull(5) ? null : reader.GetString(5),
                        KodPocztowy = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Poczta = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Gmina = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Powiat = reader.IsDBNull(9) ? null : reader.GetString(9),
                        Wojewodztwo = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Kraj = reader.IsDBNull(11) ? null : reader.GetString(11)
                    };

                    _logger.LogInformation("Adres for KRS {Krs} has been updated", krs);
                    return Ok(adres);
                }

                return StatusCode(500, "Nie udało się zaktualizować adresu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating adres for KRS: {Krs}", krs);
                return StatusCode(500, "Wystąpił błąd podczas aktualizacji adresu");
            }
        }

        /// <summary>
        /// Usuwa organizację z bazy mapowej (wraz z adresami i koordynatami)
        /// </summary>
        /// <param name="krs">Numer KRS organizacji</param>
        /// <returns>Status operacji</returns>
        [HttpDelete("map/organizations")]
        public async Task<ActionResult> DeleteMapOrganization([FromQuery] string krs)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(krs))
                {
                    return BadRequest("Parametr 'krs' jest wymagany");
                }

                _logger.LogInformation("Deleting map organization with KRS: {Krs}", krs);

                await using var connection = new NpgsqlConnection(_mapConnectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Sprawdź czy organizacja istnieje
                    const string checkSql = "SELECT numerkrs, nazwa FROM organizacja WHERE numerkrs = @krs";
                    await using var checkCommand = new NpgsqlCommand(checkSql, connection, transaction);
                    checkCommand.Parameters.AddWithValue("@krs", krs);

                    await using var reader = await checkCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        await transaction.RollbackAsync();
                        return NotFound($"Organizacja o numerze KRS '{krs}' nie została znaleziona");
                    }

                    string nazwa = reader.GetString(1);
                    await reader.CloseAsync();

                    // Zlicz ile usuwamy
                    const string countKoordSql = "SELECT COUNT(*) FROM koordynaty WHERE numerkrs = @krs";
                    await using var countKoordCommand = new NpgsqlCommand(countKoordSql, connection, transaction);
                    countKoordCommand.Parameters.AddWithValue("@krs", krs);
                    var koordCount = Convert.ToInt32(await countKoordCommand.ExecuteScalarAsync());

                    const string countAdresSql = "SELECT COUNT(*) FROM adres WHERE numerkrs = @krs";
                    await using var countAdresCommand = new NpgsqlCommand(countAdresSql, connection, transaction);
                    countAdresCommand.Parameters.AddWithValue("@krs", krs);
                    var adresCount = Convert.ToInt32(await countAdresCommand.ExecuteScalarAsync());

                    // Usuń wszystkie koordynaty
                    const string deleteKoordSql = "DELETE FROM koordynaty WHERE numerkrs = @krs";
                    await using var deleteKoordCommand = new NpgsqlCommand(deleteKoordSql, connection, transaction);
                    deleteKoordCommand.Parameters.AddWithValue("@krs", krs);
                    await deleteKoordCommand.ExecuteNonQueryAsync();

                    // Usuń wszystkie adresy
                    const string deleteAdresSql = "DELETE FROM adres WHERE numerkrs = @krs";
                    await using var deleteAdresCommand = new NpgsqlCommand(deleteAdresSql, connection, transaction);
                    deleteAdresCommand.Parameters.AddWithValue("@krs", krs);
                    await deleteAdresCommand.ExecuteNonQueryAsync();

                    // Usuń powiązania z kategoriami
                    const string deleteOrgKatSql = "DELETE FROM organizacjakategoria WHERE numerkrs = @krs";
                    await using var deleteOrgKatCommand = new NpgsqlCommand(deleteOrgKatSql, connection, transaction);
                    deleteOrgKatCommand.Parameters.AddWithValue("@krs", krs);
                    await deleteOrgKatCommand.ExecuteNonQueryAsync();

                    // Usuń organizację
                    const string deleteOrgSql = "DELETE FROM organizacja WHERE numerkrs = @krs";
                    await using var deleteOrgCommand = new NpgsqlCommand(deleteOrgSql, connection, transaction);
                    deleteOrgCommand.Parameters.AddWithValue("@krs", krs);
                    await deleteOrgCommand.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Organization '{Nazwa}' (KRS: {Krs}) has been deleted with {KoordCount} koordynatów and {AdresCount} adresów", 
                        nazwa, krs, koordCount, adresCount);
                    
                    return Ok(new 
                    { 
                        message = $"Organizacja '{nazwa}' została usunięta wraz z {koordCount} koordynatami i {adresCount} adresami",
                        organization = nazwa,
                        koordynatyDeleted = koordCount,
                        adresyDeleted = adresCount
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting map organization with KRS: {Krs}", krs);
                return StatusCode(500, "Wystąpił błąd podczas usuwania organizacji");
            }
        }
    }

    // DTOs
    public class UpdateCategoryDto
    {
        public string OldName { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
    }

    public class CreateTagDto
    {
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
    }

    public class UpdateTagByNameDto
    {
        public string TagName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string NewName { get; set; } = string.Empty;
        public int NewCategoryId { get; set; }
    }

    // DTOs dla danych mapowych
    public class UpdateKoordynatyDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class UpdateAdresDto
    {
        public string? Ulica { get; set; }
        public string? NrDomu { get; set; }
        public string? NrLokalu { get; set; }
        public string? Miejscowosc { get; set; }
        public string? KodPocztowy { get; set; }
        public string? Poczta { get; set; }
        public string? Gmina { get; set; }
        public string? Powiat { get; set; }
        public string? Wojewodztwo { get; set; }
        public string? Kraj { get; set; }
    }
}

