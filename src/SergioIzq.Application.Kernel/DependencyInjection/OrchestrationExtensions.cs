using SergioIzq.Application.Kernel.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace SergioIzq.Application.Kernel.DependencyInjection;

public static class OrchestrationExtensions
{
    /// <summary>
    /// Registra <see cref="IEntityDependencyOrchestrator"/> → <see cref="EntityDependencyOrchestrator"/> (Scoped).
    /// </summary>
    public static IServiceCollection AddKernelDependencyOrchestration(this IServiceCollection services)
    {
        services.AddScoped<IEntityDependencyOrchestrator, EntityDependencyOrchestrator>();
        return services;
    }
}
