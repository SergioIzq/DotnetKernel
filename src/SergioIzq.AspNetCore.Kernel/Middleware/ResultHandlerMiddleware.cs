using System.Text.Json;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SergioIzq.AspNetCore.Kernel.Middleware;

/// <summary>
/// Corrige automáticamente el status HTTP cuando un controlador devuelve 200 OK pero el cuerpo
/// es en realidad un <see cref="Result"/> fallido (ej. un <c>Ok(result)</c> genérico sin pasar
/// por <c>AbsController.HandleResult</c>). Solo actúa sobre respuestas JSON no comprimidas.
/// </summary>
public sealed class ResultHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResultHandlerMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public ResultHandlerMiddleware(RequestDelegate next, ILogger<ResultHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            if (HasJsonContentType(context) && !IsCompressed(context))
            {
                responseBody.Seek(0, SeekOrigin.Begin);

                if (responseBody.TryGetBuffer(out ArraySegment<byte> buffer) && buffer.Count > 0)
                {
                    if (TryDetectFailureInBody(buffer, out var error, out var suggestedStatusCode))
                    {
                        _logger.LogInformation(
                            "Result fallido detectado en middleware: {Code} - {Name} | Sugerido: {Status} | Actual: {CurrentStatus}",
                            error!.Code, error.Name, suggestedStatusCode, context.Response.StatusCode);

                        if (context.Response.StatusCode == StatusCodes.Status200OK && suggestedStatusCode != StatusCodes.Status200OK)
                        {
                            _logger.LogWarning("Corrigiendo respuesta HTTP 200 OK a {StatusCode} porque el Result contiene errores.", suggestedStatusCode);

                            await WriteErrorResponseAsync(context, originalBodyStream, error, suggestedStatusCode);
                            return;
                        }
                    }
                }
            }

            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en ResultHandlerMiddleware procesando la respuesta.");

            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static bool HasJsonContentType(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        return !string.IsNullOrEmpty(contentType) && contentType.Contains("application/json");
    }

    private static bool IsCompressed(HttpContext context)
    {
        return context.Response.Headers.ContainsKey("Content-Encoding");
    }

    /// <summary>
    /// Analiza los bytes del body para ver si es un JSON con forma { "isSuccess": false, "error": { ... } }.
    /// </summary>
    private bool TryDetectFailureInBody(ReadOnlyMemory<byte> jsonBytes, out Error? error, out int suggestedStatusCode)
    {
        error = null;
        suggestedStatusCode = StatusCodes.Status200OK;

        try
        {
            using var doc = JsonDocument.Parse(jsonBytes, DocumentOptions);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return false;

            if (!TryGetBooleanProperty(root, "isSuccess", out var isSuccess))
            {
                return false;
            }

            if (isSuccess) return false;

            if (!TryGetProperty(root, "error", out var errorElement))
            {
                return false;
            }

            var code = GetStringProperty(errorElement, "code") ?? "Error.Unknown";
            var name = GetStringProperty(errorElement, "name") ?? "Error";
            var message = GetStringProperty(errorElement, "message") ?? "Ocurrió un error no especificado";

            error = new Error(code, name, message);
            suggestedStatusCode = MapErrorCodeToHttpStatus(code);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Stream outputStream, Error error, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.ContentLength = null;

        var result = Result.Failure(error);

        await JsonSerializer.SerializeAsync(outputStream, result, JsonOptions);
    }

    private static bool TryGetBooleanProperty(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (element.TryGetProperty(propertyName, out var prop) ||
            element.TryGetProperty(ToPascalCase(propertyName), out prop))
        {
            if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
            {
                value = prop.GetBoolean();
                return true;
            }
        }
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value)) return true;
        if (element.TryGetProperty(ToPascalCase(propertyName), out value)) return true;

        value = default;
        return false;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (TryGetProperty(element, propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static string ToPascalCase(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToUpper(str[0]) + str[1..];

    /// <summary>
    /// Mapeo heurístico basado en substrings del código de error (por si el JSON crudo no trae
    /// el enum <c>ErrorType</c>). Debe mantenerse en sintonía con el mapeo enum-based de
    /// <see cref="SergioIzq.AspNetCore.Kernel.Controllers.AbsController"/> — ambos existen porque
    /// uno trabaja sobre el objeto <c>Error</c> tipado y el otro sobre JSON ya serializado.
    /// </summary>
    private static int MapErrorCodeToHttpStatus(string errorCode)
    {
        return errorCode switch
        {
            var c when Contains(c, "Validation") || Contains(c, "NullValue") || Contains(c, "InvalidFormat") => StatusCodes.Status400BadRequest,
            var c when Contains(c, "NotFound") => StatusCodes.Status404NotFound,
            var c when Contains(c, "Conflict") || Contains(c, "Duplicate") || Contains(c, "AlreadyExists") => StatusCodes.Status409Conflict,
            var c when Contains(c, "Unauthorized") || Contains(c, "InvalidCredentials") || Contains(c, "TokenExpired") => StatusCodes.Status401Unauthorized,
            var c when Contains(c, "Forbidden") || Contains(c, "AccessDenied") => StatusCodes.Status403Forbidden,
            var c when Contains(c, "Unavailable") || Contains(c, "ConnectionError") => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static bool Contains(string source, string toCheck) =>
        source.Contains(toCheck, StringComparison.OrdinalIgnoreCase);
}
