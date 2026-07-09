using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

/// <summary>
/// Handler genérico para actualizar entidades con patrón Template Method.
/// </summary>
public abstract class AbsUpdateCommandHandler<TEntity, TId, TDto, TCommand>
    : AbsCommandHandler<TEntity, TId>, IRequestHandler<TCommand, Result<Guid>>
    where TEntity : AbsEntity<TId>
    where TCommand : AbsUpdateCommand<TEntity, TId, TDto>
    where TId : IGuidValueObject
    where TDto : class
{
    protected AbsUpdateCommandHandler(
        IUnitOfWork unitOfWork,
        IWriteRepository<TEntity, TId> writeRepository,
        ICacheService cacheService,
        IUserContext userContext)
        : base(unitOfWork, writeRepository, cacheService, userContext)
    {
    }

    #region Hooks para personalizar

    /// <summary>
    /// HOOK 1: validación pre-actualización (opcional), antes de cargar la entidad de la BD.
    /// </summary>
    protected virtual Task<Result> ValidateBeforeUpdateAsync(TCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// HOOK 2: preparación de dependencias (opcional). Por defecto retorna diccionario vacío.
    /// </summary>
    protected virtual Task<Result<Dictionary<string, object>>> PrepareDependenciesAsync(
        TCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new Dictionary<string, object>()));
    }

    /// <summary>
    /// HOOK 2.5: persistir dependencias antes de actualizar la entidad principal (opcional).
    /// </summary>
    protected virtual bool ShouldPersistDependenciesFirst()
    {
        return false;
    }

    /// <summary>
    /// HOOK 3 (REQUERIDO): aplica los cambios del comando sobre la entidad ya cargada.
    /// </summary>
    protected abstract void ApplyChanges(TEntity entity, TCommand command, Dictionary<string, object>? dependencies = null);

    /// <summary>
    /// HOOK 4: validación con la entidad ya modificada, y opcionalmente marcarla como actualizada
    /// en el mismo paso. Si se implementa y actualiza, debe devolver EntityUpdated=true.
    /// </summary>
    protected virtual Task<(Result ValidationResult, bool EntityUpdated)> ValidateAndUpdateInContextAsync(
        TEntity entity,
        TCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult((Result.Success(), false));
    }

    /// <summary>
    /// HOOK 5: acciones post-actualización (opcional).
    /// </summary>
    protected virtual Task OnEntityUpdatedAsync(TEntity entity, Guid entityId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Flujo principal

    public virtual async Task<Result<Guid>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await ValidateBeforeUpdateAsync(command, cancellationToken);
            if (validationResult.IsFailure)
            {
                return Result.Failure<Guid>(validationResult.Error);
            }

            var dependenciesResult = await PrepareDependenciesAsync(command, cancellationToken);
            if (dependenciesResult.IsFailure)
            {
                return Result.Failure<Guid>(dependenciesResult.Error);
            }

            if (ShouldPersistDependenciesFirst())
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var entity = await _writeRepository.GetByIdAsync(command.Id, cancellationToken);
            if (entity is null)
            {
                return Result.Failure<Guid>(Error.NotFound(
                    $"{typeof(TEntity).Name} con ID '{command.Id}' no encontrada."));
            }

            ApplyChanges(entity, command, dependenciesResult.Value);

            var validationTuple = await ValidateAndUpdateInContextAsync(entity, command, cancellationToken);
            if (validationTuple.ValidationResult.IsFailure)
            {
                return Result.Failure<Guid>(validationTuple.ValidationResult.Error);
            }

            if (!validationTuple.EntityUpdated)
            {
                _writeRepository.Update(entity);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await OnEntityUpdatedAsync(entity, entity.Id.Value, cancellationToken);

            return Result.Success(entity.Id.Value);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(Error.Validation(ex.Message));
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(Error.Failure(
                "Error.Unexpected",
                "Error inesperado al actualizar la entidad",
                ex.Message));
        }
    }

    #endregion
}
