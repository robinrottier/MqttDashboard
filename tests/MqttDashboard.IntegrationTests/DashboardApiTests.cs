using System.Net;
using System.Net.Http.Json;

namespace MqttDashboard.IntegrationTests;

/// <summary>REST API integration tests using <see cref="IntegrationWebApplicationFactory"/>.</summary>
public class DashboardApiTests : IClassFixture<IntegrationWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DashboardApiTests(IntegrationWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsResponse()
    {
        // Health check returns 200 (healthy) or 503 (degraded/unhealthy due to no MQTT broker in tests).
        // Either way, the endpoint must respond — not 404.
        var response = await _client.GetAsync("/healthz");
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboardList_Returns200()
    {
        // Route: GET /api/dashboard/list
        var response = await _client.GetAsync("/api/dashboard/list");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDefaultDashboard_Returns200OrNotFound()
    {
        // Route: GET /api/dashboard — returns 200 with content, or 404 if no dashboard saved yet.
        var response = await _client.GetAsync("/api/dashboard");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status: {response.StatusCode}");
    }
}
