namespace MqttDashboard.IntegrationTests;

/// <summary>
/// Stub fixture for Tier B (in-process MQTT broker) tests.
///
/// MQTTnet v5 removed the embedded MQTT server from the main package. The server component
/// is now in a separate package (e.g. MQTTnet.Extensions.Hosting).  Until that package is
/// added, Tier B tests are skipped via <c>[Fact(Skip = ...)]</c> on each test method.
///
/// To enable Tier B: add the appropriate MQTTnet server package, implement InitializeAsync
/// to start a broker on <see cref="Port"/>, and remove the Skip attributes from
/// <see cref="MqttFlowIntegrationTests"/>.
/// </summary>
public sealed class InProcessMqttBrokerFixture : IAsyncLifetime
{
    /// <summary>The TCP port the in-process broker would listen on.</summary>
    public int Port { get; private set; }

    public Task InitializeAsync()
    {
        Port = FindFreePort();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
