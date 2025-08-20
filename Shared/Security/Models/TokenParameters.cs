namespace Shared.Security.Models;

public class TokenParameters
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}