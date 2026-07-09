using SergioIzq.Domain.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

/// <summary>
/// Unit of Work con gestión de transacciones y rollback automático, compatible con estrategias
/// de reintento de EF Core (ej. <c>EnableRetryOnFailure</c>).
///
/// A diferencia de la versión original de Kash, este UnitOfWork <b>no</b> despacha eventos de
/// dominio — eso lo hace únicamente <see cref="Interceptors.DomainEventDispatcherInterceptor"/>,
/// registrado como interceptor de EF Core, que se dispara solo tras confirmar que el guardado
/// tuvo éxito. Kash tenía ambos mecanismos activos a la vez, publicando cada evento dos veces
/// (y el de aquí los publicaba antes de saber si el guardado iba a funcionar) — se corrigió al extraer.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(DbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Guarda los cambios con gestión automática de transacciones y rollback.
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
