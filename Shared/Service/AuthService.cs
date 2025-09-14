using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Interface;
using Shared.Models.AuthUser;
using Shared.Security.Interfaces;

namespace Shared.Security.Services
{
    public class AuthService : IAuthService
    {
        private readonly IJwtTokenService _jwtService;
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<UserModel> _passwordHasher;

        public AuthService(IJwtTokenService jwtService, AppDbContext context, IPasswordHasher<UserModel> passwordHasher)
        {
            _jwtService = jwtService;
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<bool> RegisterAsync(UserModel model)
        {
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                return false;

            var user = new UserModel
            {
                Username = model.Username,
                Role = model.Role
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.PasswordHash);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<string> LoginAsync(LoginModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            if (user == null)
                return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.PasswordHash);
            if (result == PasswordVerificationResult.Failed)
                return null;

            var token = _jwtService.GenerateToken(
                userId: user.Id.ToString(),
                roles: new List<string> { user.Role }
            );

            return token;
        }
    }
}
