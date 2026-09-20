using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Projects.Controllers;
using Backend.Modules.Projects.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests.Projects;

public class PortfoliosControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PortfoliosController _controller;

    public PortfoliosControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _controller = new PortfoliosController(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(PortfoliosController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private async Task<User> AddDirectorAsync()
    {
        var director = new User { FullName = "Director", Email = "d@test.com", KeycloakId = "kc-d", Role = GlobalRole.PortfolioDirector };
        _db.Users.Add(director);
        await _db.SaveChangesAsync();
        return director;
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDirectorNotFoundOrWrongRole()
    {
        var result = await _controller.Create(new CreatePortfolioRequest { Name = "P1", PortfolioDirectorId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsOk_WhenDirectorValid()
    {
        var director = await AddDirectorAsync();

        var result = await _controller.Create(new CreatePortfolioRequest { Name = "P1", Description = "Desc", PortfolioDirectorId = director.Id });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Portfolios.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetAll_ReturnsPortfoliosWithProgress()
    {
        var director = await AddDirectorAsync();
        var portfolio = new Portfolio { Name = "P1", PortfolioDirectorId = director.Id };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var portfolio = new Portfolio { Name = "P1" };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();

        var result = await _controller.GetById(portfolio.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignDirector_ReturnsNotFound_WhenPortfolioMissing()
    {
        var result = await _controller.AssignDirector(Guid.NewGuid(), new AssignDirectorRequest { PortfolioDirectorId = Guid.NewGuid() });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AssignDirector_ReturnsBadRequest_WhenDirectorInvalid()
    {
        var portfolio = new Portfolio { Name = "P1" };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();

        var result = await _controller.AssignDirector(portfolio.Id, new AssignDirectorRequest { PortfolioDirectorId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AssignDirector_ReturnsOk_WhenValid()
    {
        var portfolio = new Portfolio { Name = "P1" };
        _db.Portfolios.Add(portfolio);
        var director = await AddDirectorAsync();

        var result = await _controller.AssignDirector(portfolio.Id, new AssignDirectorRequest { PortfolioDirectorId = director.Id });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Portfolios.FindAsync(portfolio.Id))!.PortfolioDirectorId.Should().Be(director.Id);
    }

    [Fact]
    public async Task GetMyPortfolios_ReturnsUnauthorized_WhenNoClaim()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetMyPortfolios();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMyPortfolios_ReturnsNotFound_WhenUserMissing()
    {
        SetUser(_controller, "unknown-kc");

        var result = await _controller.GetMyPortfolios();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyPortfolios_ReturnsOk_WithPortfolios()
    {
        var director = await AddDirectorAsync();
        _db.Portfolios.Add(new Portfolio { Name = "Mine", PortfolioDirectorId = director.Id });
        _db.Portfolios.Add(new Portfolio { Name = "Other", PortfolioDirectorId = Guid.NewGuid() });
        await _db.SaveChangesAsync();
        SetUser(_controller, director.KeycloakId);

        var result = await _controller.GetMyPortfolios();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task GetDirectors_ReturnsOk_WithDirectorsOnly()
    {
        await AddDirectorAsync();
        _db.Users.Add(new User { FullName = "Consultant", Email = "c@test.com", KeycloakId = "kc-c", Role = GlobalRole.Consultant });
        await _db.SaveChangesAsync();

        var result = await _controller.GetDirectors();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Update(Guid.NewGuid(), new UpdatePortfolioRequest { Name = "New" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_ReturnsOk_UpdatesFields()
    {
        var portfolio = new Portfolio { Name = "Old", Description = "OldDesc" };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();

        var result = await _controller.Update(portfolio.Id, new UpdatePortfolioRequest { Name = "New", Description = "NewDesc" });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await _db.Portfolios.FindAsync(portfolio.Id);
        updated!.Name.Should().Be("New");
        updated.Description.Should().Be("NewDesc");
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_ReturnsOk_RemovesPortfolio()
    {
        var portfolio = new Portfolio { Name = "P1" };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(portfolio.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Portfolios.FindAsync(portfolio.Id)).Should().BeNull();
    }
}
