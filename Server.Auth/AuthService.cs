using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Server.Core.Interfaces;

namespace Server.Auth;

public class AuthService : IAuthService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthService(string secretKey, string issuer = "SyncServer", string audience = "SyncClient")
    {
        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
    }

    public Task<AuthResult> AuthenticateAsync(string apiKey, CancellationToken ct)
    {
        // Для упрощения: любой API ключ длиной > 10 символов считается валидным
        // В продакшене нужно проверять по базе данных
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 10)
        {
            return Task.FromResult(new AuthResult
            {
                Success = false,
                ErrorMessage = "Неверный API ключ"
            });
        }

        var token = GenerateTokenAsync(apiKey, ct).Result;

        return Task.FromResult(new AuthResult
        {
            Success = true,
            Token = token
        });
    }

    public Task<string> GenerateTokenAsync(string clientId, CancellationToken ct)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, clientId),
            new Claim("client_id", clientId)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return Task.FromResult(tokenString);
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}