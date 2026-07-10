using System.Linq.Expressions;
using System.Reflection;
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

    /// <summary>
    /// Escanea los ensamblados indicados y registra en Mapster, para cada tipo que implemente
    /// <see cref="IGuidValueObject"/>, las dos direcciones de mapeo: VO→Guid (vía <c>Value</c>)
    /// y Guid→VO (vía el método estático convencional <c>CreateFromDatabase(Guid)</c>).
    /// Sustituye el bloque manual de <c>config.NewConfig&lt;XxxId, Guid&gt;()...</c> por tipo,
    /// y no puede quedarse desincronizado cuando se añade un Id nuevo al dominio.
    /// Los tipos sin <c>CreateFromDatabase</c> solo registran la dirección VO→Guid.
    /// </summary>
    public static TypeAdapterConfig RegisterGuidValueObjects(this TypeAdapterConfig config, params Assembly[] assemblies)
    {
        var registerMethod = typeof(MapsterKernelExtensions)
            .GetMethod(nameof(RegisterIdPair), BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (var assembly in assemblies)
        {
            var idTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IGuidValueObject).IsAssignableFrom(t));

            foreach (var idType in idTypes)
            {
                registerMethod.MakeGenericMethod(idType).Invoke(null, [config]);
            }
        }

        return config;
    }

    private static void RegisterIdPair<TId>(TypeAdapterConfig config) where TId : IGuidValueObject
    {
        config.NewConfig<TId, Guid>().MapWith(src => src.Value);

        var createFromDatabase = typeof(TId).GetMethod(
            "CreateFromDatabase",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(Guid)],
            modifiers: null);

        if (createFromDatabase == null || createFromDatabase.ReturnType != typeof(TId))
        {
            return;
        }

        // Expresión compilable una sola vez: src => TId.CreateFromDatabase(src)
        var param = Expression.Parameter(typeof(Guid), "src");
        var call = Expression.Call(createFromDatabase, param);
        var lambda = Expression.Lambda<Func<Guid, TId>>(call, param);

        config.NewConfig<Guid, TId>().MapWith(lambda);
    }
}
