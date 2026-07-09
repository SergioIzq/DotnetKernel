using System.Diagnostics;
using System.Text.Json;
using SergioIzq.Domain.Kernel.Abstractions.Errors;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace SergioIzq.AspNetCore.Kernel.Middleware;

/// <summary>
/// Middleware que atrapa cualquier excepción no manejada y la convierte en una respuesta JSON
/// con la forma <see cref="Result"/> y el código HTTP correspondiente. Asume MySQL para el
/// mapeo de <see cref="MySqlException"/> — es la única pieza de este paquete acoplada a un
/// motor de base de datos concreto, a propósito (ver README del kernel).
/// </summary>
public sealed class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var stopwatch = Stopwatch.StartNew();

        var (statusCode, domainError) = MapExceptionToError(exception);

        LogException(exception, context, statusCode, domainError);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var result = Result.Failure(domainError);

        await JsonSerializer.SerializeAsync(context.Response.Body, result, JsonOptions);

        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds > 500)
        {
            _logger.LogDebug("Respuesta de excepción enviada en {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private (int statusCode, Error error) MapExceptionToError(Exception exception)
    {
        return exception switch
        {
            MySqlException mySqlEx => HandleMySqlException(mySqlEx),

            ArgumentNullException argNull => (StatusCodes.Status400BadRequest,
                Error.Validation($"El parámetro '{argNull.ParamName}' es requerido.")),

            ArgumentException arg => (StatusCodes.Status400BadRequest,
                Error.Validation(arg.Message)),

            KeyNotFoundException notFound => (StatusCodes.Status404NotFound,
                Error.NotFound(notFound.Message)),

            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                new Error("Auth.Forbidden", "Acceso Denegado", "No tienes permisos para ejecutar esta acción.")),

            TimeoutException => (StatusCodes.Status408RequestTimeout,
                new Error("Server.Timeout", "Tiempo de espera agotado", "La operación tardó demasiado.")),

            NotSupportedException notSupported => (StatusCodes.Status501NotImplemented,
                new Error("Server.NotSupported", "No soportado", notSupported.Message)),

            _ => (StatusCodes.Status500InternalServerError, SystemErrors.InternalServerError)
        };
    }

    private (int statusCode, Error error) HandleMySqlException(MySqlException mySqlEx)
    {
        return mySqlEx.ErrorCode switch
        {
            MySqlErrorCode.DuplicateKeyEntry => (StatusCodes.Status409Conflict,
                new Error("Data.Duplicate", "Registro Duplicado", "Ya existe un registro con estos datos únicos.")),

            MySqlErrorCode.RowIsReferenced or MySqlErrorCode.RowIsReferenced2 => (StatusCodes.Status409Conflict,
                new Error("Delete.Error", "Error en eliminar", "El registro no se puede eliminar porque está siendo usado por otros datos.")),

            MySqlErrorCode.UnableToConnectToHost or MySqlErrorCode.ConnectionCountError => (StatusCodes.Status503ServiceUnavailable,
                new Error("Data.Unavailable", "Servicio no disponible", "No se pudo conectar con la base de datos.")),

            _ => (StatusCodes.Status500InternalServerError,
                new Error("Data.SqlError", "Error de Base de Datos", "Ocurrió un error técnico al procesar los datos."))
        };
    }

    private void LogException(Exception exception, HttpContext context, int statusCode, Error error)
    {
        var logLevel = statusCode >= 500 ? LogLevel.Error : LogLevel.Warning;

        _logger.Log(logLevel, exception,
            "[{ErrorCode}] {ErrorName}: {ErrorMessage} | Status: {StatusCode} | Path: {Path}",
            error.Code, error.Name, error.Message, statusCode, context.Request.Path);
    }
}
