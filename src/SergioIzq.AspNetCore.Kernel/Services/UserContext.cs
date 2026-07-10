using System.Security.Claims;
using SergioIzq.Application.Kernel.Interfaces;
using Microsoft.AspNetCore.Http;

namespace SergioIzq.AspNetCore.Kernel.Services;

/// <summary>
/// Implementación de <see cref="IUserContext"/>: extrae el Id del usuario autenticado desde
/// los claims (NameIdentifier → "sub" → "uid").
/// </summary>
public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user == null || !user.Identity!.IsAuthenticated)
            {
                return null;
            }

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value
                          ?? user.FindFirst("uid")?.Value;

            if (Guid.TryParse(idClaim, out var userId))
            {
                return userId;
            }

            return null;
        }
    }
}
