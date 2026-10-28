using System.Security.Claims;
using System.Text.Json;

namespace Bookstore_API.Services;

public static class UserClaims
{
    public static bool HasBookstoreApiRole(ClaimsPrincipal user, string requiredRole)
    {
        var resourceAccess = user.FindFirst("resource_access")?.Value;

        if (string.IsNullOrWhiteSpace(resourceAccess))
            return false;

        try
        {
            using var document = JsonDocument.Parse(resourceAccess);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("bookstore-api", out var api) ||
                api.ValueKind != JsonValueKind.Object ||
                !api.TryGetProperty("roles", out var roles) ||
                roles.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return roles.EnumerateArray().Any(role =>
                role.ValueKind == JsonValueKind.String &&
                role.GetString() == requiredRole);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
