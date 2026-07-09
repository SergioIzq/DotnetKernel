using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

/// <summary>
/// Handler genérico para crear entidades con patrón Template Method.
/// Expone hooks para personalizar validación, preparación de dependencias y
/// acciones post-persistencia sin duplicar el flujo principal en cada handler concreto.
/// </summary>
public abstract class AbsCreateCommandHandler<TEntity, TId, TCommand>
    : AbsCommandHandler<TEntity, TId>, IRequestHandler<TCommand, Result<Guid>>
    where TEntity : AbsEntity<TId>
    where TCommand : AbsCreateCommand<TEntity, TId>
    where TId : IGuidValueObject
{
    protected AbsCreateCommandHandler(
        IUnitOfWork unitOfWork,
        IWriteRepository<TEntity, TId> writeRepository,
        ICacheService cacheService,
        IUserContext userContext)
        : base(unitOfWork, writeRepository, cacheService, userContext)
    {
    }

    #region Hooks para personalizar

    /// <summary>
    /// HOOK 1: Validación pre-creación (opcional). Por defecto no hace nada.
    /// </summary>
    protected virtual Task<Result> ValidateBeforeCreateAsync(TCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// HOOK 2: Preparación de dependencias (opcional). Por defecto retorna diccionario vacío.
    /// </summary>
    protected virtual Task<Result<Dictionary<string, object>>> PrepareDependenciesAsync(
        TCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success(new Dictionary<string, object>()));
    }

    /// <summary>
    /// HOOK 2.5: Persistir dependencias antes de crear la entidad principal (opcional).
    /// Evita problemas de concurrencia cuando se auto-crean varias entidades relacionadas.
    /// </summary>
    protected virtual bool ShouldPersistDependenciesFirst()
    {
        return false;
    }

    /// <summary>
    /// HOOK 3 (REQUERIDO): construye la entidad a partir del comando y las dependencias preparadas.
    /// </summary>
    protected abstract TEntity CreateEntity(TCommand command, Dictionary<string, object>? dependencies = null);

    /// <summary>
    /// HOOK 4: validación con la entidad ya creada, y opcionalmente añadirla al contexto en el mismo paso
    /// (por ejemplo, para validar unicidad). Si se implementa y añade la entidad, debe devolver EntityAdded=true.
    /// </summary>
    protected virtual Task<(Result ValidationResult, bool EntityAdded)> ValidateAndAddToContextAsync(
        TEntity entity,
        TCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult((Result.Success(), false));
    }

    /// <summary>
    /// HOOK 5: acciones post-persistencia (opcional), ej. invalidar caché adicional o publicar eventos.
    /// </summary>
    protected virtual Task OnEntityCreatedAsync(TEntity entity, Guid entityId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Flujo principal

    public virtual async Task<Result<Guid>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await ValidateBeforeCreateAsync(command, cancellationToken);
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

            var entity = CreateEntity(command, dependenciesResult.Value);

            var validationTuple = await ValidateAndAddToContextAsync(entity, command, cancellationToken);
            if (validationTuple.ValidationResult.IsFailure)
            {
                return Result.Failure<Guid>(validationTuple.ValidationResult.Error);
            }

            if (!validationTuple.EntityAdded)
            {
                _writeRepository.Add(entity);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await OnEntityCreatedAsync(entity, entity.Id.Value, cancellationToken);

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
                "Error inesperado al crear la entidad",
                ex.Message));
        }
    }

    #endregion
}
