using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

/// <summary>
/// Unit of Work con gestión de transacciones y rollback automático, compatible con estrategias
/// de reintento de EF Core (ej. <c>EnableRetryOnFailure</c>).
///
/// Los eventos de dominio se despachan <b>antes</b> de <c>SaveChangesAsync</c> y dentro de la
/// transacción: los handlers pueden modificar otras entidades trackeadas (ej. actualizar el saldo
/// de una cuenta al crear un gasto) y esos cambios se persisten en el mismo guardado de forma
/// atómica. Los eventos se limpian de las entidades ANTES de publicarse, de modo que un handler
/// que vuelva a llamar a <see cref="SaveChangesAsync"/> no re-publica los mismos eventos
/// (sin recursión). <see cref="Interceptors.DomainEventDispatcherInterceptor"/> actúa como red
/// de seguridad post-guardado para flujos que no pasen por este UnitOfWork.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly IPublisher _publisher;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(DbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    /// <summary>
    /// Despacha los eventos de dominio y guarda los cambios con gestión automática
    /// de transacciones y rollback.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var shouldCommitTransaction = false;

            if (_currentTransaction == null)
            {
                _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                shouldCommitTransaction = true;
            }

            try
            {
                await DispatchDomainEventsAsync(cancellationToken);

                var result = await _context.SaveChangesAsync(cancellationToken);

                if (shouldCommitTransaction && _currentTransaction != null)
                {
                    await _currentTransaction.CommitAsync(cancellationToken);
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }

                return result;
            }
            catch (Exception)
            {
                if (_currentTransaction != null && shouldCommitTransaction)
                {
                    await _currentTransaction.RollbackAsync(cancellationToken);
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }

                throw;
            }
        });
    }

    /// <summary>
    /// Publica los eventos de dominio pendientes de las entidades trackeadas. Se limpian de las
    /// entidades antes de publicar: si un handler llama de nuevo a SaveChangesAsync (directa o
    /// indirectamente) no vuelve a encontrarlos, evitando publicación duplicada o recursión.
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entitiesWithEvents.Count == 0)
        {
            return;
        }

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
    }

    /// <summary>
    /// Hace commit de la transacción actual si existe.
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No hay una transacción activa para hacer commit.");
        }

        try
        {
            await DispatchDomainEventsAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Hace rollback de la transacción actual.
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
        }
    }
}
