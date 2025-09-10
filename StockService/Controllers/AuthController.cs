using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;
using Shared.Models.AuthUser;
using Shared.Security.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{     private readonly IJwtTokenService _jwtService;
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<UserModel> _passwordHasher;

    public AuthController(IJwtTokenService jwtService, AppDbContext context, IPasswordHasher<UserModel> passwordHasher)
    {
        _jwtService = jwtService;
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // ----------------------
    // Registro de usuário
    // ----------------------
    // ----------------------
     [HttpPost("register")]
     [AllowAnonymous]
     public async Task<IActionResult> Register([FromBody] UserModel model)
     {
         if (await _context.Users.AnyAsync(u => u.Username == model.Username))
             return BadRequest(new { message = "Usuário já existe." }); // JSON
     
         var user = new UserModel
         {
             Username = model.Username,
             Role = model.Role
         };
     
         // Hash seguro da senha
         user.PasswordHash = _passwordHasher.HashPassword(user, model.PasswordHash);
     
         _context.Users.Add(user);
         await _context.SaveChangesAsync();
     
         return Ok(new { message = "Registro efetuado com sucesso!" }); // JSON
     }

    // ----------------------
    // Login
    // ----------------------
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            if (user == null)
                return Unauthorized("Usuário ou senha inválidos.");
    
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.PasswordHash);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Usuário ou senha inválidos.");
    
            var token = _jwtService.GenerateToken(
                userId: user.Id.ToString(),
                roles: new List<string> { user.Role }
            );
    
            return Ok(new { token });
        }
        catch (Exception ex)
        {
            // Log completo do erro
            Console.WriteLine($"Erro no login: {ex}");
            return StatusCode(500, "Erro interno no servidor durante login.");
        }
    }
    
}
