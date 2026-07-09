using Microsoft.AspNetCore.Builder;

namespace SergioIzq.AspNetCore.Kernel.Middleware;

/// <summary>
/// Extensiones para registrar los middlewares del kernel en el pipeline de ASP.NET Core.
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Registra el manejo global de excepciones. Debe ser uno de los primeros middlewares del pipeline.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandler>();
    }

    /// <summary>
    /// Registra la corrección automática de status HTTP para respuestas <c>Result</c> fallidas.
    /// Debe registrarse después de <c>UseRouting()</c> pero antes de <c>UseEndpoints()</c>.
    /// </summary>
    public static IApplicationBuilder UseResultHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ResultHandlerMiddleware>();
    }

    /// <summary>
    /// Registra las cabeceras no-cache para respuestas JSON. Debe registrarse antes de
    /// <c>UseResponseCompression()</c> para que los headers se apliquen correctamente.
    /// </summary>
    public static IApplicationBuilder UseNoCache(this IApplicationBuilder app)
    {
        return app.UseMiddleware<NoCacheMiddleware>();
    }

    /// <summary>
    /// Registra los tres middlewares del kernel en el orden correcto:
    /// excepciones → corrección de Result → no-cache.
    /// </summary>
    public static IApplicationBuilder UseKernelExceptionHandling(this IApplicationBuilder app)
    {
        app.UseGlobalExceptionHandler();
        app.UseResultHandler();
        app.UseNoCache();
        return app;
    }
}
