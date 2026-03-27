using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MqttDashboard.Server.Controllers;
using MqttDashboard.Server.Models;
using MqttDashboard.Server.Services;
using Xunit;

namespace MqttDashboard.Server.Tests;

public class UpdateControllerTests
{
    // Simple handler that returns a configured response
    private class TestHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public TestHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private UpdateController CreateController(IHttpClientFactory httpFactory, IConfiguration config, ClaimsPrincipal? user = null)
    {
        var mockUpdate = new Mock<UpdateCheckService>(MockBehavior.Loose, new object[] { Mock.Of<ILogger<UpdateCheckService>>(), Mock.Of<IHttpClientFactory>() });
        var mockStorage = new Mock<DashboardStorageService>();
        var logger = Mock.Of<ILogger<UpdateController>>();
        var lifetime = Mock.Of<IHostApplicationLifetime>();

        var controller = new UpdateController(mockUpdate.Object, mockStorage.Object, logger, lifetime, config, httpFactory);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        return controller;
    }

    [Fact]
    public async Task HostUpdate_ReturnsAgentResponse_OnSuccess()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\"}")
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var handler = new TestHandler(response);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:8080/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var controller = CreateController(mockFactory.Object, config, new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }, "mock")));

        var req = new HostUpdateRequest { Service = "svc", ComposeFile = "docker-compose.yml" };
        var result = await controller.HostUpdate(req);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Contains("status", contentResult.Content);
    }

    [Fact]
    public async Task HostUpdate_ReturnsUnauthorized_WhenAdminRequiredAndNotAuthenticated()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        var handler = new TestHandler(response);
        var client = new HttpClient(handler);

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new[] { new KeyValuePair<string,string>("Auth:AdminPasswordHash","hash") }).Build();

        // User not authenticated
        var controller = CreateController(mockFactory.Object, config, new ClaimsPrincipal(new ClaimsIdentity()));

        var req = new HostUpdateRequest { Service = "svc" };
        var result = await controller.HostUpdate(req);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task HostUpdate_ReturnsBadRequest_WhenBodyMissing()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        var handler = new TestHandler(response);
        var client = new HttpClient(handler);

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var controller = CreateController(mockFactory.Object, config);

        var result = await controller.HostUpdate(null);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
