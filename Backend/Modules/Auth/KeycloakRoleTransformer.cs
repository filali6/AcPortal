using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

namespace Backend.Modules.Auth;

public class KeycloakRoleTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;

        // Cherche le claim realm_access dans le token Keycloak
        var realmAccess = identity.FindFirst("realm_access")?.Value;
        if (realmAccess is null) return Task.FromResult(principal);

        // Parse le JSON { "roles": ["Consultant", ...] }
        var parsed = JsonDocument.Parse(realmAccess);
        if (!parsed.RootElement.TryGetProperty("roles", out var roles))
            return Task.FromResult(principal);

        // Ajoute chaque rôle comme claim standard .NET
        foreach (var role in roles.EnumerateArray())
        {
            var roleName = role.GetString();
            if (roleName is not null && !identity.HasClaim(ClaimTypes.Role, roleName))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
            }
        }

        return Task.FromResult(principal);
    }
}