using System.Reflection;
using SergioIzq.Application.Kernel.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace SergioIzq.Application.Kernel.DependencyInjection;

/// <summary>
/// Registro automático por reflexión de clases que implementan <see cref="IApplicationService"/>,
/// <see cref="ITransientService"/> o <see cref="ISingletonService"/>, sin tener que dar de alta
/// cada servicio a mano. Excluye deliberadamente los requests/handlers de MediatR (esos los
/// registra <c>AddMediatR</c>, no este escáner).
/// </summary>
public static class MarkerServiceCollectionExtensions
{
    /// <summary>
    /// Escanea los ensamblados indicados y registra cada clase marcada con el lifetime
    /// correspondiente a su interfaz marcadora.
    /// </summary>
    public static IServiceCollection AddMarkedServices(this IServiceCollection services, params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            services.RegisterMarkedServices(assembly);
        }

        return services;
    }

    private static IServiceCollection RegisterMarkedServices(this IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => !IsMediatRRequestOrHandler(t))
            .ToList();

        foreach (var implementationType in types)
        {
            var allInterfaces = implementationType.GetInterfaces().ToList();
            var hasMarker = allInterfaces.Any(IsOrImplementsMarkerInterface);
            if (!hasMarker) continue;

            var interfacesToRegister = allInterfaces
                .Where(i => !IsMarkerInterface(i) && !IsSystemInterface(i) && !IsMediatRInterface(i))
                .ToList();
            if (!interfacesToRegister.Any()) continue;

            var lifetime = DetermineLifetime(allInterfaces);
            foreach (var interfaceType in interfacesToRegister)
            {
                services.Add(new ServiceDescriptor(interfaceType, implementationType, lifetime));
            }
        }

        return services;
    }

    private static bool IsMediatRRequestOrHandler(Type type)
    {
        var interfaces = type.GetInterfaces();

        if (interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName?.Contains("IRequest") == true))
            return true;

        if (interfaces.Any(i => i.FullName?.Contains("IBaseRequest") == true))
            return true;

        if (interfaces.Any(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition().FullName?.Contains("IRequestHandler") == true ||
             i.GetGenericTypeDefinition().FullName?.Contains("INotificationHandler") == true)))
            return true;

        return false;
    }

    private static bool IsMediatRInterface(Type type) => type.FullName?.StartsWith("MediatR") == true;

    private static bool IsOrImplementsMarkerInterface(Type type)
    {
        if (IsMarkerInterface(type)) return true;
        var baseInterfaces = type.GetInterfaces();
        return baseInterfaces.Any(IsMarkerInterface);
    }

    private static ServiceLifetime DetermineLifetime(List<Type> interfaces)
    {
        foreach (var interfaceType in interfaces)
        {
            var baseInterfaces = interfaceType.GetInterfaces();

            if (baseInterfaces.Contains(typeof(ISingletonService)) || interfaceType == typeof(ISingletonService))
                return ServiceLifetime.Singleton;

            if (baseInterfaces.Contains(typeof(ITransientService)) || interfaceType == typeof(ITransientService))
                return ServiceLifetime.Transient;

            if (baseInterfaces.Contains(typeof(IApplicationService)) || interfaceType == typeof(IApplicationService))
                return ServiceLifetime.Scoped;
        }

        return ServiceLifetime.Scoped;
    }

    private static bool IsMarkerInterface(Type type) =>
        type == typeof(IApplicationService) || type == typeof(ITransientService) || type == typeof(ISingletonService);

    private static bool IsSystemInterface(Type type) =>
        type == typeof(IDisposable) || type.Namespace?.StartsWith("System") == true;
}
