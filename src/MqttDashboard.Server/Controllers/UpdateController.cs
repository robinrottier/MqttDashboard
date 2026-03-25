using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using MqttDashboard.Server.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/update")]
public class UpdateController : ControllerBase
{
    private readonly UpdateCheckService _updateService;
    private readonly DashboardStorageService _diagramStorage;
    private readonly ILogger<UpdateController> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IConfiguration _configuration;

    public UpdateController(UpdateCheckService updateService, DashboardStorageService diagramStorage,
        ILogger<UpdateController> logger, IHostApplicationLifetime lifetime, IConfiguration configuration)
    {
        _updateService = updateService;
        _diagramStorage = diagramStorage;
        _logger = logger;
        _lifetime = lifetime;
        _configuration = configuration;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var info = _updateService.UpdateInfo;
        return Ok(new
        {
            currentVersion = info.CurrentVersion,
            latestVersion = info.LatestVersion,
            updateAvailable = info.UpdateAvailable,
            deploymentType = info.DeploymentType.ToString(),
            lastChecked = info.LastChecked,
            releaseUrl = info.ReleaseUrl,
            runtimeIdentifier = info.RuntimeIdentifier,
            machineName = Environment.MachineName,
            dataDirectory = _diagramStorage.StoragePath,
            dotNetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        });
    }

    [HttpPost("check")]
    public async Task<IActionResult> CheckNow()
    {
        await _updateService.CheckNowAsync();
        return GetStatus();
    }

    /// <summary>
    /// Gracefully stops the application. Under Docker with restart:always, the container
    /// restarts automatically — picking up a newly pulled image if one is available.
    /// For standalone deployments, use the download endpoint instead.
    /// </summary>
    [HttpPost("restart")]
    public IActionResult RestartApp()
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });

        _logger.LogInformation("Application restart requested from web UI");
        // Small delay so the HTTP response can be sent before shutdown begins
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            _lifetime.StopApplication();
        });
        return Ok(new { success = true, message = "Application is shutting down. It will restart automatically." });
    }

    [HttpPost("download")]
    public async Task<IActionResult> DownloadUpdate()
    {
        var info = _updateService.UpdateInfo;
        if (!info.UpdateAvailable)
            return BadRequest(new { error = "No update available." });
        if (info.DeploymentType != DeploymentType.Standalone)
            return BadRequest(new { error = "Self-update is only available for standalone deployments." });
        if (string.IsNullOrEmpty(info.DownloadAssetUrl))
            return BadRequest(new { error = "No download URL available for this runtime." });

        try
        {
            var updatesDir = Path.Combine(AppContext.BaseDirectory, "updates");
            Directory.CreateDirectory(updatesDir);
            var zipPath = Path.Combine(updatesDir, "mqttdashboard-update.zip");

            _logger.LogInformation("Downloading update from {Url}", info.DownloadAssetUrl);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "MqttDashboard-Updater/1.0");
            var bytes = await http.GetByteArrayAsync(info.DownloadAssetUrl);
            await System.IO.File.WriteAllBytesAsync(zipPath, bytes);

            // Extract to updates/new/
            var extractDir = Path.Combine(updatesDir, "new");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Write update script
            var currentExe = Environment.ProcessPath ?? "MqttDashboard.WebApp";
            var newExe = Path.Combine(extractDir, Path.GetFileName(currentExe));
            string scriptPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath = Path.Combine(updatesDir, "update.bat");
                var bat = $"""
                @echo off
                echo Stopping application...
                taskkill /F /PID {Environment.ProcessId} >nul 2>&1
                timeout /t 2 /nobreak >nul
                echo Applying update...
                copy /Y "{newExe}" "{currentExe}"
                echo Update applied. Restart the application.
                pause
                """;
                await System.IO.File.WriteAllTextAsync(scriptPath, bat);
            }
            else
            {
                scriptPath = Path.Combine(updatesDir, "update.sh");
                var sh = $"""
                #!/usr/bin/env bash
                set -e
                echo "Stopping application (PID {Environment.ProcessId})..."
                kill {Environment.ProcessId} 2>/dev/null || true
                sleep 2
                echo "Applying update..."
                cp -f "{newExe}" "{currentExe}"
                chmod +x "{currentExe}"
                echo "Update applied. Restart the application with:"
                echo "  {currentExe}"
                """;
                await System.IO.File.WriteAllTextAsync(scriptPath, sh);
                try
                {
                    System.IO.File.SetUnixFileMode(scriptPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                catch { /* ignore on platforms that don't support it */ }
            }

            var scriptName = Path.GetFileName(scriptPath);
            return Ok(new
            {
                success = true,
                updatesDir,
                scriptName,
                instructions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? $"Update downloaded. Run '.\\updates\\{scriptName}' to apply (this will stop the app)."
                    : $"Update downloaded. Run './updates/{scriptName}' to apply (this will stop the app)."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update download failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
