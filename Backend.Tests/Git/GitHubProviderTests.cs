using Backend.Modules.Git.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Backend.Tests.Git;

// GitHubProvider talks to the real GitHub API via Octokit with no injectable HTTP client,
// so these tests focus on the "not configured" no-op behavior, which is fully exercisable
// without network access.
public class GitHubProviderTests
{
    private static IConfiguration CreateConfig(string? token = null, string? organization = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Git:GitHub:Token"] = token,
                ["Git:GitHub:Organization"] = organization
            })
            .Build();

    [Fact]
    public void Constructor_DoesNotThrow_WhenTokenAndOrganizationAreMissing()
    {
        var act = () => new GitHubProvider(CreateConfig());

        act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateRepoAsync_ReturnsEmptyString_WhenNotConfigured()
    {
        var provider = new GitHubProvider(CreateConfig());

        var result = await provider.CreateRepoAsync(Guid.NewGuid(), "stream", Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PushFileAsync_ReturnsEmptyString_WhenNotConfigured()
    {
        var provider = new GitHubProvider(CreateConfig());

        var result = await provider.PushFileAsync(Guid.NewGuid(), "step", "tool", "file.txt", "content");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilesAsync_ReturnsEmptyList_WhenNotConfigured()
    {
        var provider = new GitHubProvider(CreateConfig());

        var result = await provider.GetFilesAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepoUrlAsync_ReturnsEmptyString_WhenNotConfigured()
    {
        var provider = new GitHubProvider(CreateConfig());

        var result = await provider.GetRepoUrlAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTagAsync_DoesNotThrow_WhenNotConfigured()
    {
        var provider = new GitHubProvider(CreateConfig());

        var act = async () => await provider.CreateTagAsync(Guid.NewGuid(), "v1.0", "release");

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null, "org")]
    [InlineData("token", null)]
    [InlineData("", "")]
    public async Task CreateRepoAsync_ReturnsEmptyString_WhenOnlyPartiallyConfigured(string? token, string? organization)
    {
        var provider = new GitHubProvider(CreateConfig(token, organization));

        var result = await provider.CreateRepoAsync(Guid.NewGuid(), "stream", Guid.NewGuid());

        result.Should().BeEmpty();
    }
}
