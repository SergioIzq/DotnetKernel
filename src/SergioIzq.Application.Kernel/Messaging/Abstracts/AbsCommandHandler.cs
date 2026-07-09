using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Messaging.Abstracts.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts;

public abstract class AbsCommandHandler<TEntity, TId> : IAbsCommandHandlerBase<TEntity, TId>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
{
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IWriteRepository<TEntity, TId> _writeRepository;
    protected readonly ICacheService _cacheService;
    protected readonly IUserContext _userContext;
    protected readonly ILogger? _logger;

    protected AbsCommandHandler(
        IUnitOfWork unitOfWork,
        IWriteRepository<TEntity, TId> writeRepository,
        ICacheService cacheService,
        IUserContext userContext,
        ILogger? logger = null)
    {
        _unitOfWork = unitOfWork;
        _writeRepository = writeRepository;
        _cacheService = cacheService;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<Result<Guid>> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _writeRepository.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(entity.Id.Value);
        return Result.Success(entity.Id.Value);
    }

    public async Task<Result<Guid>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _writeRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(entity.Id.Value);
        return Result.Success(entity.Id.Value);
    }

    public async Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _writeRepository.Delete(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(entity.Id.Value);
        return Result.Success();
    }

    protected async Task InvalidateCacheAsync(Guid id)
    {
        var entityName = typeof(TEntity).Name;
        var individualKey = $"{entityName}:{id}";
        await _cacheService.RemoveAsync(individualKey);
        _logger?.LogInformation("Cache individual invalidado: {CacheKey}", individualKey);

        if (_userContext.UserId.HasValue)
        {
            var versionKey = $"list_version:{entityName}:{_userContext.UserId}";
            await _cacheService.RemoveAsync(versionKey);
            _logger?.LogInformation("Version de lista invalidada: {VersionKey} para usuario {UserId}", versionKey, _userContext.UserId);
        }
        else
        {
            _logger?.LogWarning("No se pudo invalidar cache de lista porque UserId es null");
        }
    }
}
