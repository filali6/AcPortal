using System.Text;
using Backend.Data;
using Backend.Modules.Contracts.Models;
using Backend.Modules.Contracts.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Contracts;

public class ContractsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly string _uploadsRoot;
    private readonly ContractsService _service;

    public ContractsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _uploadsRoot = Path.Combine(Path.GetTempPath(), "AcPortalTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_uploadsRoot);
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_uploadsRoot);

        _service = new ContractsService(_db, NullLogger<ContractsService>.Instance, envMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_uploadsRoot))
            Directory.Delete(_uploadsRoot, recursive: true);
    }

    private static IFormFile CreateFormFile(string content = "dummy pdf content", string fileName = "test.pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    [Fact]
    public async Task CreateAsync_PersistsContract_AndSavesFilesToDisk()
    {
        var dafUserId = Guid.NewGuid();

        var contract = await _service.CreateAsync("Acme", "desc", dafUserId, new List<IFormFile> { CreateFormFile() });

        contract.FilesPaths.Should().ContainSingle();
        File.Exists(Path.Combine(_uploadsRoot, "uploads", contract.FilesPaths[0])).Should().BeTrue();
        (await _db.Contracts.FindAsync(contract.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetMyContractsAsync_ReturnsOnlyMatchingUser_OrderedByCreatedAtDescending()
    {
        var dafUserId = Guid.NewGuid();
        _db.Contracts.Add(new Contract { ClientName = "Old", DafUserId = dafUserId, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        _db.Contracts.Add(new Contract { ClientName = "New", DafUserId = dafUserId, CreatedAt = DateTime.UtcNow });
        _db.Contracts.Add(new Contract { ClientName = "Other", DafUserId = Guid.NewGuid() });
        await _db.SaveChangesAsync();

        var result = await _service.GetMyContractsAsync(dafUserId);

        result.Should().HaveCount(2);
        result[0].ClientName.Should().Be("New");
        result[1].ClientName.Should().Be("Old");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddFilesAsync_ReturnsNull_WhenContractNotFound()
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_uploadsRoot);

        var result = await _service.AddFilesAsync(Guid.NewGuid(), new List<IFormFile> { CreateFormFile() }, envMock.Object);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddFilesAsync_AppendsFiles_WhenContractExists()
    {
        var contract = new Contract { ClientName = "Acme" };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(_uploadsRoot);

        var result = await _service.AddFilesAsync(contract.Id, new List<IFormFile> { CreateFormFile(fileName: "extra.pdf") }, envMock.Object);

        result!.FilesPaths.Should().ContainSingle(p => p.EndsWith("extra.pdf"));
    }

    [Fact]
    public async Task LinkProjectAsync_SetsProjectId_AndStatus()
    {
        var contract = new Contract { ClientName = "Acme" };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        var projectId = Guid.NewGuid();

        await _service.LinkProjectAsync(contract.Id, projectId);

        var updated = await _db.Contracts.FindAsync(contract.Id);
        updated!.ProjectId.Should().Be(projectId);
        updated.Status.Should().Be(ContractStatus.ProjectCreated);
    }

    [Fact]
    public async Task LinkProjectAsync_NoOp_WhenContractNotFound()
    {
        var act = async () => await _service.LinkProjectAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        var dafUserId = Guid.NewGuid();
        _db.Contracts.Add(new Contract { ClientName = "A", DafUserId = dafUserId, ProjectId = Guid.NewGuid() });
        _db.Contracts.Add(new Contract { ClientName = "B", DafUserId = dafUserId });
        _db.Contracts.Add(new Contract { ClientName = "C", DafUserId = dafUserId });
        await _db.SaveChangesAsync();

        var (total, projectCreated, pending) = await _service.GetStatsAsync(dafUserId);

        total.Should().Be(3);
        projectCreated.Should().Be(1);
        pending.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenContractNotFound()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), "Name", "Desc", 0, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_AndAppendsNewFiles()
    {
        var contract = new Contract { ClientName = "Old", Description = "Old desc", Status = ContractStatus.Signed };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        var result = await _service.UpdateAsync(
            contract.Id, "New", "New desc", (int)ContractStatus.InProgress, new List<IFormFile> { CreateFormFile(fileName: "new.pdf") });

        result!.ClientName.Should().Be("New");
        result.Description.Should().Be("New desc");
        result.Status.Should().Be(ContractStatus.InProgress);
        result.FilesPaths.Should().ContainSingle(p => p.EndsWith("new.pdf"));
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFileFromListAndDisk()
    {
        var contract = await _service.CreateAsync("Acme", "desc", Guid.NewGuid(), new List<IFormFile> { CreateFormFile() });
        var fileName = contract.FilesPaths[0];
        var filePath = Path.Combine(_uploadsRoot, "uploads", fileName);
        File.Exists(filePath).Should().BeTrue();

        var result = await _service.DeleteFileAsync(contract.Id, fileName);

        result!.FilesPaths.Should().NotContain(fileName);
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsNull_WhenContractNotFound()
    {
        var result = await _service.DeleteFileAsync(Guid.NewGuid(), "missing.pdf");

        result.Should().BeNull();
    }
}
