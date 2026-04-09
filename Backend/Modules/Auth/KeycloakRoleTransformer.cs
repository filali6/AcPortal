using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

namespace Backend.Modules.Auth;

public class KeycloakRoleTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;

        // Debug — voir tous les claims reçus
        // foreach (var claim in identity.Claims)
        // {
        //     Console.WriteLine($"CLAIM: {claim.Type} = {claim.Value}");
        // }

        // Cherche realm_access
        var realmAccess = identity.FindFirst("realm_access")?.Value;
        if (realmAccess == null) return Task.FromResult(principal);

        try
        {
            var parsed = JsonDocument.Parse(realmAccess);
            if (!parsed.RootElement.TryGetProperty("roles", out var roles))
                return Task.FromResult(principal);

            foreach (var role in roles.EnumerateArray())
            {
                var roleName = role.GetString();
                if (roleName != null && !identity.HasClaim(ClaimTypes.Role, roleName))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                    //Console.WriteLine($"ROLE AJOUTÉ: {roleName}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERREUR TRANSFORMER: {ex.Message}");
        }

        return Task.FromResult(principal);
    }
}