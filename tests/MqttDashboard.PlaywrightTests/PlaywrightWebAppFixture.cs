using System.Diagnostics;
using Microsoft.Playwright;

namespace MqttDashboard.PlaywrightTests;

/// <summary>
/// xUnit class fixture that starts a real Kestrel instance of the server-only app
/// on a random port, initialises a headless Chromium browser, then tears both down
/// after the test class completes.
///
/// Uses <c>dotnet run --project &lt;path&gt;</c> to start the server so the test
/// works from any working directory (VS Test Explorer, <c>dotnet test</c>, CI).
///
/// One-time browser setup (run once after build):
///   <code>pwsh bin/Debug/net10.0/playwright.ps1 install chromium</code>
/// </summary>
public sealed class PlaywrightWebAppFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private readonly string _tempDataDir =
        Path.Combine(Path.GetTempPath(), "pw_mqttdashboard_" + Guid.NewGuid().ToString("N"));

    public string BaseUrl { get; private set; } = "";
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDataDir);

        var port = FindFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var serverProjectPath = FindServerProjectPath();

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            // --no-build: the server project is built as a dependency of this test project,
            // so we don't need dotnet run to rebuild it. Avoids long rebuild and prevents
            // stdout/stderr buffering issues during a slow build phase.
            Arguments = $"run --no-build --project \"{serverProjectPath}\" --no-launch-profile -- --urls \"{BaseUrl}\"",
            UseShellExecute = false,
            // DO NOT redirect stdout/stderr without reading them. If the server writes
            // more output than fits in the pipe buffer (~4 KB on Windows), Kestrel blocks
            // while trying to write, and all subsequent HTTP requests hang.
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        // Add env overrides to the inherited environment (don't replace it).
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Test";
        psi.Environment["DiagramStorage__DataDirectory"] = _tempDataDir;
        // Point at a non-existent broker so MqttClientService fails fast / silently.
        psi.Environment["MqttSettings__Broker"] = "127.0.0.1";
        psi.Environment["MqttSettings__Port"] = "19999";

        _serverProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start server process.");

        await WaitForServerAsync(TimeSpan.FromSeconds(60));

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.DisposeAsync();
        Playwright?.Dispose();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill(entireProcessTree: true);
            _serverProcess.Dispose();
        }

        if (Directory.Exists(_tempDataDir))
            try { Directory.Delete(_tempDataDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task WaitForServerAsync(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // Any HTTP response (even 503 from the MQTT health check) means
                // Kestrel is listening and the app is ready to serve requests.
                await http.GetAsync($"{BaseUrl}/healthz");
                return;
            }
            catch { /* not ready yet — connection refused */ }

            if (_serverProcess?.HasExited == true)
                throw new InvalidOperationException(
                    "Server process exited before becoming healthy.");

            await Task.Delay(500);
        }
        throw new TimeoutException($"Server at {BaseUrl} did not respond within {timeout}.");
    }

    private static string FindServerProjectPath()
    {
        // Walk up from the test output directory until we find the solution root (contains *.slnx).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.slnx").Any())
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException(
                "Could not find solution root from " + AppContext.BaseDirectory);

        return Path.Combine(
            dir.FullName, "src", "MqttDashboard.WebApp", "MqttDashboard.WebAppServerOnly");
    }

    private static int FindFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
