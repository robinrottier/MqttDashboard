using MqttDashboard.Models;

namespace MqttDashboard.Services;

public interface IDiagramService
{
    Task<DiagramState?> LoadDiagramAsync();
    Task<List<string>> ListDiagramsAsync();
    Task<DiagramState?> LoadDiagramByNameAsync(string name);
    Task<bool> SaveDiagramAsync(DiagramState diagramState);
    Task<bool> SaveDiagramByNameAsync(string name, DiagramState diagramState);
}
