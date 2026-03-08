using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SetupController> _logger;

    public SetupController(IWebHostEnvironment env, IConfiguration configuration, ILogger<SetupController> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("needed")]
    public IActionResult IsSetupNeeded()
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        return Ok(new { needed = string.IsNullOrWhiteSpace(hash) });
    }

    [HttpPost("password")]
    public IActionResult SetPassword([FromBody] SetPasswordRequest request)
    {
        var existingHash = _configuration["Auth:AdminPasswordHash"];
        if (!string.IsNullOrWhiteSpace(existingHash))
            return Conflict(new { error = "Admin password is already configured." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 10);

        var userSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.user.json");
        var settings = new Dictionary<string, object>
        {
            ["Auth"] = new Dictionary<string, string>
            {
                ["AdminPasswordHash"] = hash
            }
        };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(userSettingsPath, json);

        _logger.LogInformation("Admin password configured and saved to appsettings.user.json");

        if (_configuration is IConfigurationRoot configRoot)
            configRoot.Reload();

        return Ok(new { success = true });
    }
}

public record SetPasswordRequest(string Password);
