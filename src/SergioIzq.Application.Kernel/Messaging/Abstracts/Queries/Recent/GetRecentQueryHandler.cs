using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;

/// <summary>
/// Handler base para obtener elementos recientes, con caché corta (30s) y resultados limitados.
/// </summary>
public abstract class GetRecentQueryHandler<TEntity, TDto, TId, TQuery>
    : AbsQueryHandler<TEntity, TId>, IRequestHandler<TQuery, Result<IEnumerable<TDto>>>
    where TEntity : AbsEntity<TId>
    where TQuery : GetRecentQuery<TEntity, TDto, TId>
    where TDto : class
    where TId : IGuidValueObject
{
    protected readonly IReadRepository<TEntity, TDto, TId> _repository;

    protected GetRecentQueryHandler(
        IReadRepository<TEntity, TDto, TId> repository,
        ICacheService cacheService)
        : base(cacheService)
    {
        _repository = repository;
    }

    protected virtual Dictionary<string, object>? GetCustomFilters(TQuery query) => null;

    protected virtual string GetCacheKeySuffix(TQuery query) => string.Empty;

    public virtual async Task<Result<IEnumerable<TDto>>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        if (!query.UsuarioId.HasValue)
        {
            return Result.Failure<IEnumerable<TDto>>(Error.Validation("El ID de usuario es requerido."));
        }

        var cacheSuffix = GetCacheKeySuffix(query);
        string cacheKey = $"{typeof(TEntity).Name}:recent:{query.UsuarioId}:{query.Limit}{cacheSuffix}";

        var cachedResult = await _cacheService.GetAsync<IEnumerable<TDto>>(cacheKey);
        if (cachedResult != null)
        {
            return Result.Success(cachedResult);
        }

        var extraFilters = GetCustomFilters(query);

        var results = await _repository.GetRecentAsync(
            query.UsuarioId.Value,
            query.Limit,
            extraFilters,
            cancellationToken);

        await _cacheService.SetAsync(cacheKey, results, slidingExpiration: TimeSpan.FromSeconds(30));

        return Result.Success(results);
    }
}
