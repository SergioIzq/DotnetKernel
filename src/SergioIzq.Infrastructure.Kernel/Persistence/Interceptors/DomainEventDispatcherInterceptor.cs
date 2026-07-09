using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SergioIzq.Infrastructure.Kernel.Persistence.Interceptors;

/// <summary>
/// Interceptor de EF Core que publica los eventos de dominio tras confirmar que el guardado
/// tuvo éxito (<c>SavedChangesAsync</c>) — es el único punto del kernel que despacha eventos;
/// <see cref="UnitOfWork"/> deliberadamente no lo hace también (ver su comentario de clase).
/// </summary>
public sealed class DomainEventDispatcherInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventDispatcherInterceptor> _logger;
    private static readonly int MaxDegreeOfParallelism = Environment.ProcessorCount;
    private static readonly int BatchSize = 32;

    public DomainEventDispatcherInterceptor(IPublisher publisher, ILogger<DomainEventDispatcherInterceptor> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task PublishDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entitiesWithEvents = new List<(object entity, List<IDomainEvent> events)>();

        // Se itera sobre TODAS las entidades tracked (no solo las que exponen IHasDomainEvents
        // como interfaz estática) por si alguna entidad la implementa de forma duck-typed.
        foreach (var entry in context.ChangeTracker.Entries())
        {
            var domainEventsProperty = entry.Entity.GetType().GetProperty("DomainEvents");

            if (domainEventsProperty != null)
            {
                var domainEvents = domainEventsProperty.GetValue(entry.Entity) as IReadOnlyCollection<IDomainEvent>;

                if (domainEvents != null && domainEvents.Count > 0)
                {
                    var eventsCopy = new List<IDomainEvent>(domainEvents);
                    entitiesWithEvents.Add((entry.Entity, eventsCopy));

                    _logger.LogDebug(
                        "Encontrados {EventCount} eventos de dominio en entidad {EntityType}",
                        eventsCopy.Count,
                        entry.Entity.GetType().Name);
                }
            }
        }

        if (entitiesWithEvents.Count == 0)
        {
            _logger.LogDebug("No se encontraron eventos de dominio para publicar");
            return;
        }

        _logger.LogInformation(
            "Publicando {EventCount} eventos de dominio de {EntityCount} entidades",
            entitiesWithEvents.Sum(e => e.events.Count),
            entitiesWithEvents.Count);

        using var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);
        var publishTasks = new List<Task>(entitiesWithEvents.Count);

        foreach (var (_, events) in entitiesWithEvents)
        {
            foreach (var domainEvent in events)
            {
                await semaphore.WaitAsync(cancellationToken);

                var publishTask = PublishEventWithSemaphoreAsync(domainEvent, semaphore, cancellationToken);
                publishTasks.Add(publishTask);

                if (publishTasks.Count >= BatchSize)
                {
                    await Task.WhenAll(publishTasks);
                    publishTasks.Clear();
                }
            }
        }

        if (publishTasks.Count > 0)
        {
            await Task.WhenAll(publishTasks);
        }

        foreach (var (entity, _) in entitiesWithEvents)
        {
            var clearMethod = entity.GetType().GetMethod("ClearDomainEvents");
            clearMethod?.Invoke(entity, null);
        }

        _logger.LogInformation("Eventos de dominio publicados exitosamente");
    }

    private async Task PublishEventWithSemaphoreAsync(
        IDomainEvent domainEvent,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Publicando evento de dominio: {EventType}", domainEvent.GetType().Name);

            await _publisher.Publish(domainEvent, cancellationToken);

            _logger.LogDebug("Evento de dominio publicado exitosamente: {EventType}", domainEvent.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al publicar evento de dominio: {EventType}", domainEvent.GetType().Name);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }
}
