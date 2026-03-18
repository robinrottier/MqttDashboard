using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace MqttDashboard.Server.Filters;

/// <summary>
/// Action filter that returns 401 for unauthenticated requests when auth is configured.
/// When Auth:AdminPasswordHash is not set, all requests are allowed.
/// </summary>
public class RequireAdminFilter : IActionFilter
{
    private readonly IConfiguration _configuration;

    public RequireAdminFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled) return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            context.Result = new UnauthorizedObjectResult(new { error = "Admin authentication required" });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
