using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Auth.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Auth;

public class AuthServiceTests : IDisposable
{
    private const string BaseUrl = "http://keycloak.test";
    private const string Realm = "acp";

    private readonly AppDbContext _db;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // Routes requests by method + path so a single handler can stand in for the whole Keycloak admin API.
    private class RoutingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public RoutingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:BaseUrl"] = BaseUrl,
                ["Keycloak:Realm"] = Realm,
                ["Keycloak:AdminUsername"] = "admin",
                ["Keycloak:AdminPassword"] = "admin"
            })
            .Build();

    private static HttpResponseMessage TokenResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { access_token = "admin-token" }), Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage CreateUserResponse(string keycloakId)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"{BaseUrl}/admin/realms/{Realm}/users/{keycloakId}");
        return response;
    }

    private static HttpResponseMessage RoleResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { id = "role-id", name = "Consultant" }), Encoding.UTF8, "application/json")
    };

    private AuthService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder, IConfiguration? config = null)
    {
        var handler = new RoutingHttpMessageHandler(responder);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return new AuthService(_db, config ?? CreateConfig(), NullLogger<AuthService>.Instance, factory.Object);
    }

    private static HttpResponseMessage HappyPathResponder(HttpRequestMessage request, string keycloakId)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/protocol/openid-connect/token")) return TokenResponse();
        if (request.Method == HttpMethod.Post && path.EndsWith("/users")) return CreateUserResponse(keycloakId);
        if (request.Method == HttpMethod.Get && path.Contains("/roles/")) return RoleResponse();
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsNull_WhenEmailAlreadyExists()
    {
        _db.Users.Add(new User { FullName = "Existing", Email = "dup@test.com", KeycloakId = "kc-1" });
        await _db.SaveChangesAsync();

        var service = CreateService(req => throw new InvalidOperationException("HTTP should not be called"));

        var result = await service.RegisterAsync("New", "dup@test.com", "pwd", GlobalRole.Consultant);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_ReturnsNull_WhenAdminTokenRequestFails()
    {
        var service = CreateService(req => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await service.RegisterAsync("New", "new@test.com", "pwd", GlobalRole.Consultant);

        result.Should().BeNull();
        (await _db.Users.AnyAsync(u => u.Email == "new@test.com")).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_ReturnsNull_WhenKeycloakUserCreationFails()
    {
        var service = CreateService(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token")
                ? TokenResponse()
                : new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await service.RegisterAsync("New", "new@test.com", "pwd", GlobalRole.Consultant);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_CreatesLocalUser_WhenKeycloakFlowSucceeds()
    {
        const string keycloakId = "kc-123";
        var service = CreateService(req => HappyPathResponder(req, keycloakId));

        var result = await service.RegisterAsync("New User", "new@test.com", "pwd", GlobalRole.Consultant);

        result.Should().NotBeNull();
        result!.KeycloakId.Should().Be(keycloakId);
        result.Email.Should().Be("new@test.com");
        (await _db.Users.SingleAsync(u => u.Email == "new@test.com")).KeycloakId.Should().Be(keycloakId);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ReturnsFalse_WhenAdminTokenFails()
    {
        var service = CreateService(req => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await service.UpdateUserRoleAsync("kc-1", "Consultant", "ProjectManager");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ReturnsTrue_WhenAdminTokenSucceeds()
    {
        var service = CreateService(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token") ? TokenResponse() : RoleResponse());

        var result = await service.UpdateUserRoleAsync("kc-1", "Consultant", "ProjectManager");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsFalse_WhenAdminTokenFails()
    {
        var service = CreateService(req => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await service.DeleteUserAsync("kc-1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsTrue_WhenKeycloakDeleteSucceeds()
    {
        var service = CreateService(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token")
                ? TokenResponse()
                : new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await service.DeleteUserAsync("kc-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsFalse_WhenKeycloakDeleteFails()
    {
        var service = CreateService(req =>
            req.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token")
                ? TokenResponse()
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.DeleteUserAsync("kc-1");

        result.Should().BeFalse();
    }
}
