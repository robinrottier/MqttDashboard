using MqttDashboard.Server.Services;
using MqttDashboard.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace MqttDashboard.Server.Tests;

public class DiagramStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DiagramStorageService _service;

    public DiagramStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DiagramStorage:DataDirectory"] = _tempDir })
            .Build();

        _service = new DiagramStorageService(env.Object, config, NullLogger<DiagramStorageService>.Instance);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var state = new DiagramState { Name = "Test Diagram" };
        state.Nodes.Add(new NodeState { Id = "n1", Title = "Node 1", X = 10, Y = 20, Width = 120, Height = 90 });

        var saved = await _service.SaveDiagramAsync(state);
        Assert.True(saved);

        var loaded = await _service.LoadDiagramAsync();
        Assert.NotNull(loaded);
        Assert.Equal("Test Diagram", loaded!.Name);
        Assert.Single(loaded.Nodes);
        Assert.Equal("Node 1", loaded.Nodes[0].Title);
    }

    [Fact]
    public async Task Load_WhenNoFile_ReturnsNull()
    {
        var loaded = await _service.LoadDiagramAsync();
        Assert.Null(loaded);
    }

    [Fact]
    public async Task ConcurrentSaves_DoNotCorruptData()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _service.SaveDiagramAsync(new DiagramState { Name = $"Diagram {i}" }));
        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r));
        // File should be valid JSON — load should succeed
        var loaded = await _service.LoadDiagramAsync();
        Assert.NotNull(loaded);
    }

    [Fact]
    public void StoragePath_UsesConfiguredDirectory()
    {
        Assert.Equal(_tempDir, _service.StoragePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
