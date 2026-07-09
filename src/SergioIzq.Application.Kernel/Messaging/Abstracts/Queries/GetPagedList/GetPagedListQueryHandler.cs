using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;

/// <summary>
/// Handler base para consultas paginadas. Pagina a nivel de base de datos (no en memoria) y
/// cachea por versión de lista: cualquier Create/Update/Delete invalida la versión, no cada key individual.
/// </summary>
public abstract class GetPagedListQueryHandler<TEntity, TId, TDto, TQuery>
    : AbsQueryHandler<TEntity, TId>, IRequestHandler<TQuery, Result<PagedList<TDto>>>
    where TEntity : AbsEntity<TId>
    where TQuery : AbsGetPagedListQuery<TEntity, TId, TDto>
    where TDto : class
    where TId : IGuidValueObject
{
    protected readonly IReadRepository<TEntity, TDto, TId> _dtoRepository;

    protected GetPagedListQueryHandler(
        IReadRepository<TEntity, TDto, TId> dtoRepository,
        ICacheService cacheService)
        : base(cacheService)
    {
        _dtoRepository = dtoRepository;
    }

    /// <summary>
    /// Override para aplicar filtros adicionales antes de paginar. Si no se sobrescribe,
    /// se usa la paginación directa del repositorio.
    /// </summary>
    protected virtual Task<PagedList<TDto>> ApplyFiltersAsync(
        TQuery query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<PagedList<TDto>>(null!);
    }

    public virtual async Task<Result<PagedList<TDto>>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        string versionKey = $"list_version:{typeof(TEntity).Name}:{query.UsuarioId}";
        string? listVersion = await _cacheService.GetAsync<string>(versionKey);

        if (string.IsNullOrEmpty(listVersion))
        {
            listVersion = Guid.NewGuid().ToString();
            await _cacheService.SetAsync(
                versionKey,
                listVersion,
                slidingExpiration: TimeSpan.FromMinutes(2),
                absoluteExpiration: TimeSpan.FromMinutes(3));
        }

        string cacheKey = $"{typeof(TEntity).Name}:paged:{query.UsuarioId}:{listVersion}:{query.Page}:{query.PageSize}:{query.SearchTerm}:{query.SortColumn}:{query.SortOrder}";

        var cachedResult = await _cacheService.GetAsync<PagedList<TDto>>(cacheKey);
        if (cachedResult != null)
        {
            return Result.Success(cachedResult);
        }

        var customFiltered = await ApplyFiltersAsync(query, cancellationToken);
        if (customFiltered != null)
        {
            await _cacheService.SetAsync(
                cacheKey,
                customFiltered,
                slidingExpiration: TimeSpan.FromMinutes(1),
                absoluteExpiration: TimeSpan.FromMinutes(2));

            return Result.Success(customFiltered);
        }

        PagedList<TDto> pagedDtos;

        if (query.UsuarioId.HasValue)
        {
            pagedDtos = await _dtoRepository.GetPagedReadModelsByUserAsync(
                query.UsuarioId.Value,
                query.Page,
                query.PageSize,
                query.SearchTerm,
                query.SortColumn,
                query.SortOrder,
                cancellationToken);
        }
        else
        {
            pagedDtos = await _dtoRepository.GetPagedReadModelsAsync(
                query.Page,
                query.PageSize,
                cancellationToken);
        }

        await _cacheService.SetAsync(
            cacheKey,
            pagedDtos,
            slidingExpiration: TimeSpan.FromMinutes(1),
            absoluteExpiration: TimeSpan.FromMinutes(2));

        return Result.Success(pagedDtos);
    }
}
