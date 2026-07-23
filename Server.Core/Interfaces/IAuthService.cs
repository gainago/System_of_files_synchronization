namespace Server.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> AuthenticateAsync(string apiKey, CancellationToken ct);
    Task<string> GenerateTokenAsync(string clientId, CancellationToken ct);
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct);
}

public record AuthResult
{
    public bool Success { get; init; }
    public string? Token { get; init; }
    public string? ErrorMessage { get; init; }
}