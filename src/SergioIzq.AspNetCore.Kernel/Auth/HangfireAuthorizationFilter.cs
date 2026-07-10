using Hangfire.Dashboard;

namespace SergioIzq.AspNetCore.Kernel.Auth;

/// <summary>
/// Filtro de autorización para el dashboard de Hangfire: acceso libre en Development,
/// denegado en cualquier otro entorno (el dashboard con ASP.NET Core no tiene acceso
/// directo al HttpContext autenticado, así que en producción se bloquea directamente).
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    }
}
