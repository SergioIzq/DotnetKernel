using SergioIzq.Domain.Kernel.Abstractions.Enums;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SergioIzq.AspNetCore.Kernel.Controllers;

/// <summary>
/// Controlador base para APIs CQRS con MediatR: traduce <see cref="Result"/>/<see cref="Result{T}"/>
/// a respuestas HTTP con el código correcto, y expone helpers de usuario autenticado/cookies.
/// </summary>
[Authorize]
[ApiController]
public abstract class AbsController : ControllerBase
{
    protected readonly ISender _sender;

    protected AbsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Retorna 200 OK con el valor si es exitoso, o el error correspondiente.</summary>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            return HandleFailure(result.Error);
        }

        return Ok(result);
    }

    /// <summary>
    /// Envía el request por MediatR y traduce el <see cref="Result{T}"/> a la respuesta HTTP.
    /// Colapsa el patrón repetido "Send + HandleResult" de cada endpoint a una línea.
    /// </summary>
    protected async Task<IActionResult> SendAndHandleAsync<T>(IRequest<Result<T>> request, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Envía el request por MediatR y traduce el <see cref="Result"/> (sin valor) a la respuesta
    /// HTTP: 204 No Content si es exitoso, o el error correspondiente.
    /// </summary>
    protected async Task<IActionResult> SendAndHandleAsync(IRequest<Result> request, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Retorna 204 No Content si es exitoso (ideal para Update/Delete), o el error.</summary>
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsFailure)
        {
            return HandleFailure(result.Error);
        }

        return NoContent();
    }

    /// <summary>Retorna 201 Created con cabecera Location si se provee ruta, o 201 genérico.</summary>
    protected IActionResult HandleResultForCreation<T>(Result<T> result, string? actionName = null, object? routeValues = null)
    {
        if (result.IsFailure)
        {
            return HandleFailure(result.Error);
        }

        if (!string.IsNullOrEmpty(actionName) && routeValues != null)
        {
            return CreatedAtAction(actionName, routeValues, result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Convierte un <see cref="Error"/> de dominio en la respuesta HTTP con el código correcto,
    /// mapeando desde <see cref="ErrorType"/> (no strings mágicos).
    /// </summary>
    private IActionResult HandleFailure(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var failureResult = Result.Failure(error);
        return StatusCode(statusCode, failureResult);
    }

    protected Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Obtiene el Id del usuario autenticado. Si no está presente (p.ej. un token sin el claim
    /// esperado, aunque haya pasado [Authorize]), retorna el 401 a devolver; si sí está, null.
    /// Uso: <c>if (RequireCurrentUserId(out var usuarioId) is { } unauthorized) return unauthorized;</c>
    /// </summary>
    protected IActionResult? RequireCurrentUserId(out Guid usuarioId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            usuarioId = Guid.Empty;
            return Unauthorized(Result.Failure(Error.Unauthorized("Usuario no autenticado")));
        }

        usuarioId = currentUserId.Value;
        return null;
    }

    protected void SetRefreshTokenCookie(string token, int expireDays = 7)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(expireDays),
            SameSite = SameSiteMode.Strict,
            Secure = !IsDevelopment()
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }

    private bool IsDevelopment()
    {
        return HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() ?? false;
    }
}
