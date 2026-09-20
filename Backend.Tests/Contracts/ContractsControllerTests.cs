using System.Security.Claims;
using System.Text;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Contracts.Controllers;
using Backend.Modules.Contracts.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Services;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Contracts;

public class ContractsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly string _uploadsRoot;
    private readonly ContractsController _controller;

    public ContractsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _uploadsRoot = Path.Combine(Path.GetTempPath(), "AcPortalTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_uploadsRoot);
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_uploadsRoot);

        var contractsService = new ContractsService(_db, NullLogger<ContractsService>.Instance, envMock.Object);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventPublisher:MaxRetries"] = "0"
        }).Build();
        var eventPublisher = new EventPublisher(new DaprClientBuilder().Build(), NullLogger<EventPublisher>.Instance, config);

        _controller = new ContractsController(contractsService, _db, eventPublisher, envMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_uploadsRoot))
            Directory.Delete(_uploadsRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static void SetUser(ContractsController controller, string? keycloakId)
    {
        var claims = keycloakId == null
            ? new List<Claim>()
            : new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static IFormFile CreateFormFile(string content = "dummy pdf content", string fileName = "test.pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    private async Task<User> AddDafUserAsync()
    {
        var user = new User { FullName = "Daf", Email = "daf@test.com", KeycloakId = "kc-daf", Role = GlobalRole.DAF };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenNoClaim()
    {
        SetUser(_controller, null);

        var result = await _controller.Create(new CreateContractRequest { ClientName = "Acme" });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenUserMissing()
    {
        SetUser(_controller, "unknown-kc");

        var result = await _controller.Create(new CreateContractRequest { ClientName = "Acme" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ReturnsOk_PersistsContract()
    {
        var daf = await AddDafUserAsync();
        SetUser(_controller, daf.KeycloakId);

        var result = await _controller.Create(new CreateContractRequest
        {
            ClientName = "Acme",
            Description = "Desc",
            Files = new List<IFormFile> { CreateFormFile() }
        });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Contracts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetMy_ReturnsUnauthorized_WhenNoClaim()
    {
        SetUser(_controller, null);

        var result = await _controller.GetMy();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMy_ReturnsOk_WithStatsAndContracts()
    {
        var daf = await AddDafUserAsync();
        _db.Contracts.Add(new Contract { ClientName = "Acme", DafUserId = daf.Id });
        await _db.SaveChangesAsync();
        SetUser(_controller, daf.KeycloakId);

        var result = await _controller.GetMy();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var contract = new Contract { ClientName = "Acme" };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        var result = await _controller.GetById(contract.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddFiles_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.AddFiles(Guid.NewGuid(), new AddFilesRequest());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AddFiles_ReturnsOk_WhenFound()
    {
        var contract = new Contract { ClientName = "Acme" };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        var result = await _controller.AddFiles(contract.Id, new AddFilesRequest { Files = new List<IFormFile> { CreateFormFile() } });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void DownloadFile_ReturnsNotFound_WhenFileMissing()
    {
        var result = _controller.DownloadFile("missing.pdf");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void DownloadFile_ReturnsFile_WhenExists()
    {
        var uploadsDir = Path.Combine(_uploadsRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);
        File.WriteAllText(Path.Combine(uploadsDir, "present.pdf"), "content");

        var result = _controller.DownloadFile("present.pdf");

        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Update(Guid.NewGuid(), new UpdateContractRequest { ClientName = "New" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_ReturnsOk_UpdatesFields()
    {
        var contract = new Contract { ClientName = "Old" };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        var result = await _controller.Update(contract.Id, new UpdateContractRequest { ClientName = "New", Description = "D", Status = 1 });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Contracts.FindAsync(contract.Id))!.ClientName.Should().Be("New");
    }

    [Fact]
    public async Task DeleteFile_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.DeleteFile(Guid.NewGuid(), "file.pdf");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteFile_ReturnsOk_WhenFound()
    {
        var contract = new Contract { ClientName = "Acme" };
        contract.FilesPaths.Add("file.pdf");
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        var result = await _controller.DeleteFile(contract.Id, "file.pdf");

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Contracts.FindAsync(contract.Id))!.FilesPaths.Should().NotContain("file.pdf");
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithContracts()
    {
        _db.Contracts.Add(new Contract { ClientName = "Acme" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task Summarize_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Summarize(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Summarize_ReturnsOk_WhenFound()
    {
        var contract = new Contract { ClientName = "Acme" };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        var result = await _controller.Summarize(contract.Id);

        result.Should().BeOfType<OkObjectResult>();
    }
}
