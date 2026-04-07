using Backend.Data;
using Backend.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Backend.Modules.Auth.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthService(
        AppDbContext db,
        IConfiguration config,
        ILogger<AuthService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<User?> RegisterAsync(
        string fullName, string email, string password, GlobalRole role)
    {
        // 1. Vérifier si l'email existe déjà dans ta BDD
        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists)
        {
            _logger.LogWarning("Email déjà utilisé : {Email}", email);
            return null;
        }

        // 2. Obtenir un token admin Keycloak
        var adminToken = await GetAdminTokenAsync();
        if (adminToken == null)
        {
            _logger.LogError("Impossible d'obtenir le token admin Keycloak");
            return null;
        }

        // 3. Créer le user dans Keycloak
        var keycloakId = await CreateKeycloakUserAsync(
            adminToken, fullName, email, password, role);
        if (keycloakId == null)
        {
            _logger.LogError("Échec création user dans Keycloak : {Email}", email);
            return null;
        }

        // 4. Créer le user dans ta BDD (sans password)
        var user = new User
        {
            FullName = fullName,
            Email = email,
            KeycloakId = keycloakId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User créé : {Email} / {Role}", email, role);
        return user;
    }

    // ─── Méthodes privées ───────────────────────────────────────

    private async Task<string?> GetAdminTokenAsync()
    {
        var http = _httpClientFactory.CreateClient();
        var baseUrl = _config["Keycloak:BaseUrl"];

        var response = await http.PostAsync(
            $"{baseUrl}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = _config["Keycloak:AdminUsername"]!,
                ["password"] = _config["Keycloak:AdminPassword"]!
            })
        );

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json)
            .RootElement.GetProperty("access_token").GetString();
    }

    private async Task<string?> CreateKeycloakUserAsync(
        string adminToken, string fullName,
        string email, string password, GlobalRole role)
    {
        var http = _httpClientFactory.CreateClient();
        var baseUrl = _config["Keycloak:BaseUrl"];
        var realm = _config["Keycloak:Realm"];

        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", adminToken);

        // Créer le user
        var body = new
        {
            username = email,
            email = email,
            firstName = fullName,
            lastName=".",
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new { type = "password", value = password, temporary = false }
            }
        };

        var createResponse = await http.PostAsync(
            $"{baseUrl}/admin/realms/{realm}/users",
            new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json")
        );

        if (!createResponse.IsSuccessStatusCode) return null;

        // Récupérer l'ID du user créé (dans le header Location)
        var keycloakId = createResponse.Headers.Location?.ToString().Split('/').Last();
        if (keycloakId == null) return null;

        // Assigner le rôle
        await AssignRoleAsync(http, baseUrl!, realm!, keycloakId, role.ToString());

        return keycloakId;
    }

    private async Task AssignRoleAsync(
        HttpClient http, string baseUrl,
        string realm, string keycloakId, string roleName)
    {
        // Récupérer le rôle depuis Keycloak
        var roleResponse = await http.GetAsync(
            $"{baseUrl}/admin/realms/{realm}/roles/{roleName}");

        if (!roleResponse.IsSuccessStatusCode) return;

        var roleJson = await roleResponse.Content.ReadAsStringAsync();

        // Assigner le rôle au user
        await http.PostAsync(
            $"{baseUrl}/admin/realms/{realm}/users/{keycloakId}/role-mappings/realm",
            new StringContent($"[{roleJson}]", Encoding.UTF8, "application/json")
        );
    }
}