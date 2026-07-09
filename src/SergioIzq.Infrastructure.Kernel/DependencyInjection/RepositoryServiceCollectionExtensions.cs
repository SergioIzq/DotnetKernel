using System.Reflection;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using SergioIzq.Infrastructure.Kernel.Persistence;
using SergioIzq.Infrastructure.Kernel.Persistence.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace SergioIzq.Infrastructure.Kernel.DependencyInjection;

public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Escanea los ensamblados indicados y registra automáticamente cada implementación de
    /// <c>IWriteRepository&lt;,&gt;</c>/<c>IReadRepository&lt;,,&gt;</c> encontrada, con vida Scoped.
    /// A diferencia de Kash (que usaba <c>Assembly.GetExecutingAssembly()</c>, inútil en un
    /// paquete separado), aquí el consumidor pasa explícitamente los ensamblados donde viven
    /// sus propios repositorios concretos.
    /// </summary>
    public static IServiceCollection AddKernelRepositories(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IWriteRepository<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.Where(type =>
                type.GetInterfaces().Any(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IReadRepository<,,>))))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

    /// <summary>
    /// Registra <see cref="IUnitOfWork"/> (implementado por <see cref="UnitOfWork"/>) y el
    /// <see cref="DomainEventDispatcherInterceptor"/> como servicios Scoped. El interceptor
    /// hay que añadirlo también a las opciones del <c>DbContext</c> del consumidor con
    /// <c>options.AddInterceptors(...)</c> — este método solo lo registra en el contenedor de DI.
    ///
    /// <para><b>Importante:</b> <see cref="UnitOfWork"/> pide un <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
    /// genérico por constructor. <c>AddDbContext&lt;TContext&gt;</c> solo registra el tipo concreto
    /// en el contenedor, no la clase base — hay que exponerlo también, por ejemplo:
    /// <c>services.AddScoped&lt;DbContext&gt;(sp =&gt; sp.GetRequiredService&lt;MyDbContext&gt;());</c></para>
    /// </summary>
    public static IServiceCollection AddKernelUnitOfWork(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DomainEventDispatcherInterceptor>();

        return services;
    }
}
