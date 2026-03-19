using MqttDashboard.Models;

namespace MqttDashboard.Services;

public interface IDashboardService
{
    Task<DiagramState?> LoadDashboardAsync();
    Task<List<string>> ListDashboardsAsync();
    Task<DiagramState?> LoadDashboardByNameAsync(string name);
    Task<bool> SaveDashboardAsync(DiagramState diagramState);
    Task<bool> SaveDashboardByNameAsync(string name, DiagramState diagramState);
}

