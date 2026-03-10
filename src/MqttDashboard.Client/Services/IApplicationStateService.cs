using MqttDashboard.Models;

namespace MqttDashboard.Services;

public interface IApplicationStateService
{
    Task<ApplicationStateData?> LoadStateAsync();
    Task<bool> SaveStateAsync(ApplicationStateData state);
}
