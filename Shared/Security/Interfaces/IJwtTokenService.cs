using System.Security.Claims;

namespace Shared.Security.Interfaces
{
    public interface IJwtTokenService
    {
        /// <summary>
        /// Gera um token JWT para o usuário informado, com roles e claims opcionais.
        /// </summary>
        /// <param name="userId">ID do usuário</param>
        /// <param name="roles">Roles do usuário</param>
        /// <param name="claims">Claims adicionais opcionais</param>
        /// <returns>Token JWT como string</returns>
        string GenerateToken(string userId, IEnumerable<string> roles, IDictionary<string, string>? claims = null);

        /// <summary>
        /// Valida o token JWT e retorna o ClaimsPrincipal correspondente
        /// </summary>
        /// <param name="token">Token JWT</param>
        /// <returns>ClaimsPrincipal do token</returns>
        ClaimsPrincipal ValidateToken(string token);

        /// <summary>
        /// Retorna o UserId (NameIdentifier) a partir do token JWT
        /// </summary>
        /// <param name="token">Token JWT</param>
        /// <returns>ID do usuário</returns>
        string GetUserIdFromToken(string token);

        /// <summary>
        /// Retorna todas as roles do token JWT
        /// </summary>
        /// <param name="token">Token JWT</param>
        /// <returns>Lista de roles</returns>
        IEnumerable<string> GetRolesFromToken(string token);
    }
}
