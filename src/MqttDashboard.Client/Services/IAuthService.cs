namespace MqttDashboard.Services;

public interface IAuthService
{
    Task<(bool isAdmin, bool authEnabled)> GetStatusAsync();
    Task<bool> LoginAsync(string password);
    Task LogoutAsync();
}
