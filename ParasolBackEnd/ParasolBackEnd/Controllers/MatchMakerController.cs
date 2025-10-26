using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ParasolBackEnd.DTOs;
using ParasolBackEnd.Services;

namespace ParasolBackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchMakerController : ControllerBase
    {
        private readonly IMatchMakerService _matchMakerService;
        private readonly IAuthService _authService;
        private readonly ILogger<MatchMakerController> _logger;

        public MatchMakerController(IMatchMakerService matchMakerService, IAuthService authService, ILogger<MatchMakerController> logger)
        {
            _matchMakerService = matchMakerService;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Pobiera wszystkie kategorie
        /// </summary>
        /// <returns>Lista kategorii bez tagów</returns>
        [HttpGet("categories")]
        public async Task<ActionResult<List<CategorySimpleDto>>> GetCategories()
        {
            try
            {
                var categories = await _matchMakerService.GetCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCategories");
                return StatusCode(500, "Wystąpił błąd podczas pobierania kategorii");
            }
        }

        /// <summary>
        /// Pobiera kategorię po ID
        /// </summary>
        /// <param name="id">ID kategorii</param>
        /// <returns>Kategoria z jej tagami</returns>
        [HttpGet("categories/{id}")]
        public async Task<ActionResult<CategoryDto>> GetCategory(int id)
        {
            try
            {
                var category = await _matchMakerService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    return NotFound($"Kategoria o ID {id} nie została znaleziona");
                }
                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCategory with id: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas pobierania kategorii");
            }
        }

        /// <summary>
        /// Pobiera wszystkie tagi
        /// </summary>
        /// <returns>Lista wszystkich tagów z ich kategoriami</returns>
        [HttpGet("tags")]
        public async Task<ActionResult<List<TagDto>>> GetTags()
        {
            try
            {
                var tags = await _matchMakerService.GetTagsAsync();
                return Ok(tags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTags");
                return StatusCode(500, "Wystąpił błąd podczas pobierania tagów");
            }
        }



        /// <summary>
        /// Pobiera tag po ID
        /// </summary>
        /// <param name="id">ID tagu</param>
        /// <returns>Tag z jego kategorią</returns>
        [HttpGet("tags/{id}")]
        public async Task<ActionResult<TagDto>> GetTag(int id)
        {
            try
            {
                var tag = await _matchMakerService.GetTagByIdAsync(id);
                if (tag == null)
                {
                    return NotFound($"Tag o ID {id} nie został znaleziony");
                }
                return Ok(tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTag with id: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas pobierania tagu");
            }
        }

        // Post endpoints
        /// <summary>
        /// Pobiera wszystkie posty z opcjonalnymi filtrami
        /// </summary>
        /// <param name="categoryId">ID kategorii do filtrowania</param>
        /// <param name="tagId">ID tagu do filtrowania</param>
        /// <param name="searchTerm">Termin wyszukiwania</param>
        /// <param name="page">Numer strony (domyślnie 1)</param>
        /// <param name="pageSize">Rozmiar strony (domyślnie 20)</param>
        /// <param name="includeDetails">Czy ładować szczegóły (kategorie, tagi)</param>
        /// <returns>Lista postów</returns>
        [HttpGet("posts")]
        public async Task<ActionResult<List<PostDto>>> GetPosts(
            int? categoryId = null, 
            int? tagId = null, 
            string? searchTerm = null,
            int page = 1,
            int pageSize = 20,
            bool includeDetails = false)
        {
            try
            {
                List<PostDto> posts;
                
                posts = await _matchMakerService.GetPostsSummaryAsync(categoryId, tagId, searchTerm, page, pageSize);
                
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPosts");
                return StatusCode(500, "Wystąpił błąd podczas pobierania postów");
            }
        }

        /// <summary>
        /// Pobiera post po ID
        /// </summary>
        /// <param name="id">ID posta</param>
        /// <returns>Post z kategoriami i tagami</returns>
        [HttpGet("posts/{id}")]
        public async Task<ActionResult<PostDto>> GetPost(int id)
        {
            try
            {
                var post = await _matchMakerService.GetPostByIdAsync(id);
                if (post == null)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }
                return Ok(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPost with id: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas pobierania posta");
            }
        }

        /// <summary>
        /// Pobiera wszystkie posty zalogowanej organizacji (wymaga autoryzacji)
        /// </summary>
        /// <param name="includeDetails">Czy ładować szczegóły (kategorie, tagi)</param>
        /// <returns>Lista postów zalogowanej organizacji</returns>
        [HttpGet("posts/my")]
        [Authorize]
        public async Task<ActionResult<List<PostDto>>> GetMyPosts(bool includeDetails = false)
        {
            try
            {
                // Pobierz ID organizacji z tokenu
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zobaczyć swoje ogłoszenia", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                var organizationId = _authService.GetOrganizationIdFromToken(token);
                
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby zobaczyć swoje ogłoszenia", requiresAuth = true });
                }

                var posts = await _matchMakerService.GetPostsByOrganizationAsync(organizationId.Value, includeDetails, includeDetails);
                return Ok(posts.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMyPosts");
                return StatusCode(500, "Wystąpił błąd podczas pobierania postów");
            }
        }

        /// <summary>
        /// Tworzy nowy post (wymaga autoryzacji)
        /// </summary>
        /// <param name="createPostDto">Dane do utworzenia posta</param>
        /// <returns>Utworzony post</returns>
        [HttpPost("posts")]
        [Authorize]
        public async Task<ActionResult<PostDto>> CreatePost(CreatePostDto createPostDto)
        {
            try
            {
                // Pobierz ID organizacji z tokenu
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby utworzyć ogłoszenie", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                var organizationId = _authService.GetOrganizationIdFromToken(token);
                
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby utworzyć ogłoszenie", requiresAuth = true });
                }

                // Ustaw ID organizacji z tokenu
                createPostDto.OrganizationId = organizationId.Value;

                // Walidacja wymaganych pól
                if (string.IsNullOrWhiteSpace(createPostDto.Title))
                {
                    return BadRequest("Tytuł jest wymagany");
                }

                if (string.IsNullOrWhiteSpace(createPostDto.Description))
                {
                    return BadRequest("Opis jest wymagany");
                }

                if (string.IsNullOrWhiteSpace(createPostDto.ContactEmail))
                {
                    return BadRequest("Email kontaktowy jest wymagany");
                }

                // Walidacja formatu email
                if (!System.Text.RegularExpressions.Regex.IsMatch(createPostDto.ContactEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return BadRequest("Nieprawidłowy format email");
                }

                // Walidacja daty wygaśnięcia
                if (createPostDto.ExpiresAt.HasValue)
                {
                    if (createPostDto.ExpiresAt.Value < DateOnly.FromDateTime(DateTime.Today))
                    {
                        return BadRequest("Data wygaśnięcia nie może być w przeszłości");
                    }
                }

                // Walidacja kategorii i tagów
                if (createPostDto.CategoryIds?.Any(id => id <= 0) == true)
                {
                    return BadRequest("ID kategorii muszą być większe od 0");
                }

                if (createPostDto.TagIds?.Any(id => id <= 0) == true)
                {
                    return BadRequest("ID tagów muszą być większe od 0");
                }

                var post = await _matchMakerService.CreatePostAsync(createPostDto);
                return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error in CreatePost: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreatePost");
                return StatusCode(500, "Wystąpił błąd podczas tworzenia posta");
            }
        }

        /// <summary>
        /// Aktualizuje post (wymaga autoryzacji)
        /// </summary>
        /// <param name="id">ID posta</param>
        /// <param name="updatePostDto">Dane do aktualizacji</param>
        /// <returns>Zaktualizowany post</returns>
        [HttpPut("posts/{id}")]
        [Authorize]
        public async Task<ActionResult<PostDto>> UpdatePost(int id, UpdatePostDto updatePostDto)
        {
            try
            {
                // Pobierz ID organizacji z tokenu
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby edytować ogłoszenie", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                var organizationId = _authService.GetOrganizationIdFromToken(token);
                
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby edytować ogłoszenie", requiresAuth = true });
                }

                // Sprawdź czy post należy do zalogowanej organizacji
                var existingPost = await _matchMakerService.GetPostByIdAsync(id);
                if (existingPost == null)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }

                if (existingPost.OrganizationId != organizationId.Value)
                {
                    return Forbid("Nie masz uprawnień do edycji tego ogłoszenia");
                }

                // Walidacja wymaganych pól
                if (string.IsNullOrWhiteSpace(updatePostDto.Title))
                {
                    return BadRequest("Tytuł jest wymagany");
                }

                if (string.IsNullOrWhiteSpace(updatePostDto.Description))
                {
                    return BadRequest("Opis jest wymagany");
                }

                if (string.IsNullOrWhiteSpace(updatePostDto.ContactEmail))
                {
                    return BadRequest("Email kontaktowy jest wymagany");
                }

                // Walidacja formatu email
                if (!System.Text.RegularExpressions.Regex.IsMatch(updatePostDto.ContactEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return BadRequest("Nieprawidłowy format email");
                }

                // Walidacja kategorii i tagów
                if (updatePostDto.CategoryIds?.Any(catId => catId <= 0) == true)
                {
                    return BadRequest("ID kategorii muszą być większe od 0");
                }

                if (updatePostDto.TagIds?.Any(tagId => tagId <= 0) == true)
                {
                    return BadRequest("ID tagów muszą być większe od 0");
                }

                var post = await _matchMakerService.UpdatePostAsync(id, updatePostDto);
                if (post == null)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }
                return Ok(post);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error in UpdatePost with id {Id}: {Message}", id, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePost with id: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas aktualizacji posta");
            }
        }

        /// <summary>
        /// Usuwa post (wymaga autoryzacji)
        /// </summary>
        /// <param name="id">ID posta</param>
        /// <returns>Status operacji</returns>
        [HttpDelete("posts/{id}")]
        [Authorize]
        public async Task<ActionResult> DeletePost(int id)
        {
            try
            {
                // Pobierz ID organizacji z tokenu
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby usunąć ogłoszenie", requiresAuth = true });
                }

                var token = authHeader.Substring("Bearer ".Length);
                var organizationId = _authService.GetOrganizationIdFromToken(token);
                
                if (!organizationId.HasValue)
                {
                    return Unauthorized(new { message = "Proszę się zalogować aby usunąć ogłoszenie", requiresAuth = true });
                }

                // Sprawdź czy post należy do zalogowanej organizacji
                var existingPost = await _matchMakerService.GetPostByIdAsync(id);
                if (existingPost == null)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }

                if (existingPost.OrganizationId != organizationId.Value)
                {
                    return Forbid("Nie masz uprawnień do usunięcia tego ogłoszenia");
                }

                var result = await _matchMakerService.DeletePostAsync(id);
                if (!result)
                {
                    return NotFound($"Post o ID {id} nie został znaleziony");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeletePost with id: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas usuwania posta");
            }
        }

        /// <summary>
        /// Pobiera publiczny profil organizacji po ID
        /// </summary>
        /// <param name="id">ID organizacji</param>
        /// <returns>Profil organizacji (bez emaila loginowego)</returns>
        [HttpGet("organizations/{id}")]
        public async Task<ActionResult<OrganizationPublicProfileDto>> GetOrganizationProfile(int id)
        {
            try
            {
                var profile = await _matchMakerService.GetOrganizationPublicProfileAsync(id);
                if (profile == null)
                {
                    return NotFound($"Organizacja o ID {id} nie została znaleziona");
                }
                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization profile with id: {Id}", id);
                return StatusCode(500, "Wystąpił błąd podczas pobierania profilu organizacji");
            }
        }
    }
}
