using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.AspNetCore.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SergioIzq.AspNetCore.Kernel.DependencyInjection;

/// <summary>
/// Registro de las implementaciones web de los contratos del kernel de aplicación.
/// </summary>
public static class WebServicesExtensions
{
    /// <summary>Registra <see cref="IUserContext"/> → <see cref="UserContext"/> (claims JWT).</summary>
    public static IServiceCollection AddKernelUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        return services;
    }

    /// <summary>Registra <see cref="IPasswordHasher"/> → <see cref="PasswordHasherService"/> (Identity).</summary>
    public static IServiceCollection AddKernelPasswordHasher(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        return services;
    }

    /// <summary>Registra <see cref="IFileStorageService"/> → <see cref="LocalFileStorageService"/> (wwwroot).</summary>
    public static IServiceCollection AddKernelFileStorage(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        return services;
    }
}
