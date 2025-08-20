using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shared.Security.Interfaces;

namespace Shared.Security.Services
{
    public class JwtTokenValidator : ITokenValidator
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly SymmetricSecurityKey _securityKey;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public JwtTokenValidator(string secretKey, string issuer)
        {
            _secretKey = secretKey;
            _issuer = issuer;
            _securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        public string GenerateToken(
            string userId,
            IEnumerable<string> roles,
            IDictionary<string, string>? additionalClaims = null)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, userId)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            if (additionalClaims != null)
                claims.AddRange(additionalClaims.Select(c => new Claim(c.Key, c.Value)));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _issuer,
                Audience = _issuer,
                SigningCredentials = new SigningCredentials(
                    _securityKey,
                    SecurityAlgorithms.HmacSha256)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            return _tokenHandler.WriteToken(token);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _securityKey,
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _issuer,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return _tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }

        public string? GetUserIdFromToken(string token) =>
            GetClaimValue(token, ClaimTypes.NameIdentifier);

        public IEnumerable<string> GetRolesFromToken(string token) =>
            GetClaims(token, ClaimTypes.Role).Select(c => c.Value);

        public string? GetClaimValue(string token, string claimType) =>
            GetClaims(token, claimType).FirstOrDefault()?.Value;

        public bool IsTokenExpired(string token)
        {
            try
            {
                var jwtToken = _tokenHandler.ReadJwtToken(token);
                return jwtToken.ValidTo < DateTime.UtcNow;
            }
            catch
            {
                return true;
            }
        }

        private IEnumerable<Claim> GetClaims(string token, string claimType)
        {
            if (!_tokenHandler.CanReadToken(token))
                return Enumerable.Empty<Claim>();

            var jwtToken = _tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims.Where(c => c.Type == claimType);
        }
    }
}
