using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled)
            return Ok(new { isAdmin = true, authEnabled = false });

        return Ok(new { isAdmin = User.Identity?.IsAuthenticated == true, authEnabled = true });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrEmpty(hash))
            return Ok(new { isAdmin = true }); // auth not configured

        if (string.IsNullOrEmpty(request.Password))
            return Unauthorized(new { error = "Password required" });

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(request.Password, hash); }
        catch { valid = false; }

        if (!valid)
            return Unauthorized(new { error = "Invalid password" });

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return Ok(new { isAdmin = true });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }
}

public record LoginRequest(string Password);
