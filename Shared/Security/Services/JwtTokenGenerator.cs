using Shared.Security.Interfaces;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Shared.Security.Services;

public class JwtTokenGenerator : IJwtTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly int _expiryMinutes;

    public JwtTokenGenerator(string secretKey, string issuer, int expiryMinutes)
    {
        _secretKey = secretKey;
        _issuer = issuer;
        _expiryMinutes = expiryMinutes;
    }

    public string GenerateToken(string userId, IEnumerable<string> roles, IDictionary<string, string>? claims = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secretKey);

        var tokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId)
        };

        tokenClaims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (claims != null)
        {
            tokenClaims.AddRange(claims.Select(c => new Claim(c.Key, c.Value)));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(tokenClaims),
            Expires = DateTime.UtcNow.AddMinutes(_expiryMinutes),
            Issuer = _issuer,
            Audience = _issuer,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = _issuer,
            ValidAudience = _issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        };

        return tokenHandler.ValidateToken(token, validationParameters, out _);
    }

    public string GetUserIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        return principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

    public IEnumerable<string> GetRolesFromToken(string token)
    {
        var principal = ValidateToken(token);
        return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }
}
