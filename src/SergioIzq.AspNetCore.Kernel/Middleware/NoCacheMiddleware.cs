using Microsoft.AspNetCore.Http;

namespace SergioIzq.AspNetCore.Kernel.Middleware;

/// <summary>
/// Añade cabeceras no-cache a cualquier respuesta JSON, para que un Ctrl+R en el navegador
/// no reutilice datos de API obsoletos.
/// </summary>
public sealed class NoCacheMiddleware
{
    private readonly RequestDelegate _next;

    public NoCacheMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Se ejecuta justo antes de enviar los headers, pero después de que el controlador
        // haya definido el Content-Type.
        context.Response.OnStarting(() =>
        {
            if (IsApiResponse(context))
            {
                var headers = context.Response.Headers;

                headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                headers["Pragma"] = "no-cache";
                headers["Expires"] = "0";
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool IsApiResponse(HttpContext context)
    {
        var contentType = context.Response.ContentType;

        if (string.IsNullOrEmpty(contentType))
            return false;

        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
