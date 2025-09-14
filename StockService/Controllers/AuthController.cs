using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Interface;
using Shared.Models.AuthUser;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] UserModel model)
    {
        var success = await _authService.RegisterAsync(model);
        if (!success)
            return BadRequest(new { message = "Usuário já existe." });

        return Ok(new { message = "Registro efetuado com sucesso!" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var token = await _authService.LoginAsync(model);
        if (string.IsNullOrEmpty(token))
            return Unauthorized("Usuário ou senha inválidos.");

        return Ok(new { token });
    }
}
