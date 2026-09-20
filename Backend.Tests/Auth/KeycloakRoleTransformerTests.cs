using System.Security.Claims;
using Backend.Modules.Auth;
using FluentAssertions;
using Xunit;

namespace Backend.Tests.Auth;

public class KeycloakRoleTransformerTests
{
    private readonly KeycloakRoleTransformer _transformer = new();

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task TransformAsync_ReturnsUnchanged_WhenNoRealmAccessClaim()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "kc-1"));

        var result = await _transformer.TransformAsync(principal);

        result.Should().BeSameAs(principal);
        result.Claims.Should().ContainSingle();
    }

    [Fact]
    public async Task TransformAsync_AddsRoleClaims_FromRealmAccessJson()
    {
        var principal = CreatePrincipal(new Claim("realm_access", "{\"roles\":[\"HeadOfCDS\",\"SuperAdmin\"]}"));

        var result = await _transformer.TransformAsync(principal);

        result.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo("HeadOfCDS", "SuperAdmin");
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicateExistingRoleClaim()
    {
        var principal = CreatePrincipal(
            new Claim("realm_access", "{\"roles\":[\"HeadOfCDS\"]}"),
            new Claim(ClaimTypes.Role, "HeadOfCDS"));

        var result = await _transformer.TransformAsync(principal);

        result.Claims.Count(c => c.Type == ClaimTypes.Role && c.Value == "HeadOfCDS").Should().Be(1);
    }

    [Fact]
    public async Task TransformAsync_ReturnsUnchanged_WhenRealmAccessHasNoRolesProperty()
    {
        var principal = CreatePrincipal(new Claim("realm_access", "{\"other\":true}"));

        var result = await _transformer.TransformAsync(principal);

        result.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task TransformAsync_ReturnsPrincipalUnchanged_WhenRealmAccessIsInvalidJson()
    {
        var principal = CreatePrincipal(new Claim("realm_access", "not-json"));

        var result = await _transformer.TransformAsync(principal);

        result.Should().BeSameAs(principal);
        result.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
    }
}
