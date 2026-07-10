using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Infrastructure.Kernel.Caching;
using SergioIzq.Infrastructure.Kernel.Email;
using SergioIzq.Infrastructure.Kernel.Persistence;
using SergioIzq.Infrastructure.Kernel.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SergioIzq.Infrastructure.Kernel.DependencyInjection;

/// <summary>
/// Registro de las implementaciones de servicios del kernel. Extensiones granulares:
/// cada app registra solo lo que usa.
/// </summary>
public static class KernelServicesCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="ICacheService"/> → <see cref="DistributedCacheService"/> (Scoped).
    /// Requiere un IDistributedCache registrado aparte (AddDistributedMemoryCache, Redis...).
    /// </summary>
    public static IServiceCollection AddKernelCache(this IServiceCollection services)
    {
        services.AddScoped<ICacheService, DistributedCacheService>();
        return services;
    }

    /// <summary>
    /// Registra el pipeline de email en cola: <see cref="EmailSettings"/> desde configuración,
    /// <see cref="QueuedEmailService"/> como singleton (y como <see cref="IEmailService"/>),
    /// y <see cref="EmailBackgroundSender"/> como hosted service que drena la cola.
    /// </summary>
    public static IServiceCollection AddKernelEmail(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = EmailSettings.SectionName)
    {
        services.Configure<EmailSettings>(configuration.GetSection(sectionName));
        services.AddSingleton<QueuedEmailService>();
        services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<QueuedEmailService>());
        services.AddHostedService<EmailBackgroundSender>();
        return services;
    }

    /// <summary>
    /// Registra <see cref="IJobSchedulingService"/> → <see cref="JobSchedulingService"/> (Hangfire).
    /// El storage/servidor de Hangfire se configura aparte (ej. AddKernelHangfire en AspNetCore.Kernel).
    /// </summary>
    public static IServiceCollection AddKernelJobScheduling(this IServiceCollection services)
    {
        services.AddScoped<IJobSchedulingService, JobSchedulingService>();
        return services;
    }

    /// <summary>
    /// Registra <see cref="IDomainValidator"/> → <see cref="DapperDomainValidator"/> (Scoped).
    /// </summary>
    public static IServiceCollection AddKernelDomainValidator(this IServiceCollection services)
    {
        services.AddScoped<IDomainValidator, DapperDomainValidator>();
        return services;
    }

    /// <summary>
    /// Registra <see cref="DatabaseWarmupService"/> como hosted service.
    /// </summary>
    public static IServiceCollection AddKernelDatabaseWarmup(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseWarmupService>();
        return services;
    }
}
