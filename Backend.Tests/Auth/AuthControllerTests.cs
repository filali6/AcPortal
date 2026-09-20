using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Data;
using Backend.Modules.Auth.Controllers;
using Backend.Modules.Auth.Models;
using Backend.Modules.Auth.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Auth;

public class AuthControllerTests : IDisposable
{
    private readonly AppDbContext _db;

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // Routes any outgoing Keycloak admin HTTP call to a canned success response.
    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static HttpResponseMessage TokenResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { access_token = "admin-token" }), Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage CreateUserResponse(string keycloakId)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"http://keycloak.test/admin/realms/acp/users/{keycloakId}");
        return response;
    }

    private static HttpResponseMessage RoleResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { id = "role-id", name = "Consultant" }), Encoding.UTF8, "application/json")
    };

    private AuthController CreateController(bool keycloakSucceeds = true)
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            if (!keycloakSucceeds) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (req.RequestUri!.AbsolutePath.EndsWith("/token")) return TokenResponse();
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users")) return CreateUserResponse(Guid.NewGuid().ToString());
            if (req.RequestUri!.AbsolutePath.Contains("/roles/")) return RoleResponse();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:BaseUrl"] = "http://keycloak.test",
            ["Keycloak:Realm"] = "acp",
            ["Keycloak:AdminUsername"] = "admin",
            ["Keycloak:AdminPassword"] = "admin"
        }).Build();

        var authService = new AuthService(_db, config, NullLogger<AuthService>.Instance, factory.Object);
        return new AuthController(authService, _db);
    }

    [Fact]
    public async Task Register_WithNewEmail_ReturnsOkWithUser()
    {
        var controller = CreateController();

        var result = await controller.Register(new AuthController.RegisterRequest
        {
            FullName = "Jane Doe",
            Email = "jane@test.com",
            Password = "password1"
        });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Users.SingleAsync()).Email.Should().Be("jane@test.com");
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        _db.Users.Add(new User { FullName = "Existing", Email = "dup@test.com", KeycloakId = "kc-1" });
        await _db.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.Register(new AuthController.RegisterRequest
        {
            FullName = "Jane Doe",
            Email = "dup@test.com",
            Password = "password1"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_ReturnsAllUsers()
    {
        _db.Users.Add(new User { FullName = "A", Email = "a@test.com", KeycloakId = "kc-a" });
        _db.Users.Add(new User { FullName = "B", Email = "b@test.com", KeycloakId = "kc-b" });
        await _db.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProjectManagers_ReturnsOnlyProjectManagers()
    {
        _db.Users.Add(new User { FullName = "PM", Email = "pm@test.com", KeycloakId = "kc-pm", Role = GlobalRole.ProjectManager });
        _db.Users.Add(new User { FullName = "Consultant", Email = "c@test.com", KeycloakId = "kc-c", Role = GlobalRole.Consultant });
        await _db.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.GetProjectManagers();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateUser_WhenRegisterFails_ReturnsBadRequest()
    {
        var controller = CreateController(keycloakSucceeds: false);

        var result = await controller.CreateUser(new AuthController.RegisterRequest
        {
            FullName = "Jane",
            Email = "jane2@test.com",
            Password = "password1"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.UpdateUser(Guid.NewGuid(), new AuthController.UpdateUserRequest { FullName = "New Name" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateUser_WhenUserExists_UpdatesFullName()
    {
        var user = new User { FullName = "Old", Email = "u@test.com", KeycloakId = "kc-u" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.UpdateUser(user.Id, new AuthController.UpdateUserRequest { FullName = "New Name" });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Users.FindAsync(user.Id))!.FullName.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteUser_WhenUserNotFound_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.DeleteUser(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteUser_WhenUserExists_RemovesUser()
    {
        var user = new User { FullName = "ToDelete", Email = "del@test.com", KeycloakId = "kc-d" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.DeleteUser(user.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Users.FindAsync(user.Id)).Should().BeNull();
    }
}
