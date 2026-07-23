using Microsoft.AspNetCore.Mvc;
using Server.Core.Interfaces;

namespace SyncServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.AuthenticateAsync(request.ApiKey, ct);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(new AuthResponse
        {
            Token = result.Token!,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
    }
}

public class LoginRequest
{
    public string ApiKey { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}