using System.Security.Claims;

namespace Shared.Security.Interfaces
{
    public interface ITokenValidator
    {
        string GenerateToken(
            string userId,
            IEnumerable<string> roles,
            IDictionary<string, string>? additionalClaims = null);

        ClaimsPrincipal? ValidateToken(string token);

        string? GetUserIdFromToken(string token);

        IEnumerable<string> GetRolesFromToken(string token);

        string? GetClaimValue(string token, string claimType);

        bool IsTokenExpired(string token);
    }
}