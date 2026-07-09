using Mapster;
using SergioIzq.Domain.Kernel.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace SergioIzq.Application.Kernel.Mapping;

/// <summary>
/// Configuración global de Mapster para el kernel: enseña a desempaquetar automáticamente
/// cualquier Value Object que implemente <see cref="IGuidValueObject"/> a <see cref="Guid"/>,
/// sin tener que registrar cada tipo de Id concreto uno por uno.
/// </summary>
public static class MapsterKernelExtensions
{
    public static IServiceCollection AddApplicationKernelMapping(this IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;

        config.NewConfig<IGuidValueObject, Guid>()
              .MapWith(src => src.Value);

        return services;
    }
}
