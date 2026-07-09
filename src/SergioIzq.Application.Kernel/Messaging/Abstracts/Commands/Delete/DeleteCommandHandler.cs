using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

/// <summary>
/// Handler genérico para eliminar entidades. Construye un "stub" de la entidad con
/// solo el Id para hacer DELETE directo sin cargarla completa de la base de datos.
/// Requiere que <typeparamref name="TId"/> exponga un método estático público
/// <c>CreateFromDatabase(Guid)</c> (misma convención que <c>SergioIzq.Infrastructure.Kernel</c>).
/// </summary>
public abstract class DeleteCommandHandler<TEntity, TId, TCommand>
    : AbsCommandHandler<TEntity, TId>, IRequestHandler<TCommand, Result>
    where TEntity : AbsEntity<TId>
    where TCommand : AbsDeleteCommand<TEntity, TId>
    where TId : IGuidValueObject
{
    protected DeleteCommandHandler(
        IUnitOfWork unitOfWork,
        IWriteRepository<TEntity, TId> writeRepository,
        ICacheService cacheService,
        IUserContext userContext)
        : base(unitOfWork, writeRepository, cacheService, userContext)
    {
    }

    public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await LoadEntityForDeletionAsync(command.Id, cancellationToken);

            if (entity == null)
            {
                return Result.Failure(Error.NotFound(
                    $"Entidad {typeof(TEntity).Name} con ID '{command.Id}' no encontrada para eliminación."));
            }

            var canDeleteResult = CanDelete(entity);
            if (canDeleteResult.IsFailure)
            {
                return canDeleteResult;
            }

            var result = await DeleteAsync(entity, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.NotFound(
                $"Entidad {typeof(TEntity).Name} con ID '{command.Id}' no encontrada para eliminación."));
        }
        catch (DbUpdateException ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            return Result.Failure(Error.Conflict(
                $"No se puede eliminar porque tiene registros relacionados. Detalle: {errorMessage}"));
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure(
                "Database.Error",
                "Error de base de datos",
                ex.Message));
        }
    }

    /// <summary>
    /// Override para implementar validaciones de negocio antes de eliminar (ej. dependencias activas).
    /// </summary>
    protected virtual Result CanDelete(TEntity entity)
    {
        return Result.Success();
    }

    /// <summary>
    /// Override para cargar la entidad real en vez de un stub, útil si necesitas disparar
    /// eventos de dominio al eliminar. Por defecto crea un stub sin acceso a BD.
    /// </summary>
    protected virtual Task<TEntity?> LoadEntityForDeletionAsync(Guid id, CancellationToken cancellationToken)
    {
        var entityStub = CreateEntityStub(id);
        return Task.FromResult<TEntity?>(entityStub);
    }

    private TEntity CreateEntityStub(Guid id)
    {
        var entity = (TEntity)Activator.CreateInstance(typeof(TEntity), true)!;

        var idType = typeof(TId);
        var createFromDatabaseMethod = idType.GetMethod("CreateFromDatabase",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (createFromDatabaseMethod == null)
        {
            throw new InvalidOperationException(
                $"El tipo {idType.Name} debe tener un método estático 'CreateFromDatabase(Guid value)'.");
        }

        var valueObjectId = createFromDatabaseMethod.Invoke(null, new object[] { id });

        var idProperty = typeof(TEntity).GetProperty("Id");
        idProperty?.SetValue(entity, valueObjectId);

        return entity;
    }
}
