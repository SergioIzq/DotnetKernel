using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;

/// <summary>
/// Handler base para consultas GetById. Implementa cache-aside: primero busca en caché, luego en BD.
/// </summary>
public abstract class GetByIdQueryHandler<TEntity, TId, TDto, TQuery>
    : AbsQueryHandler<TEntity, TId>, IRequestHandler<TQuery, Result<TDto>>
    where TEntity : AbsEntity<TId>
    where TDto : class
    where TQuery : AbsGetByIdQuery<TEntity, TId, TDto>
    where TId : IGuidValueObject
{
    protected readonly IReadRepository<TEntity, TDto, TId> _dtoRepository;

    protected GetByIdQueryHandler(
        IReadRepository<TEntity, TDto, TId> dtoRepository,
        ICacheService cacheService)
        : base(cacheService)
    {
        _dtoRepository = dtoRepository;
    }

    public async Task<Result<TDto>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        string cacheKey = $"{typeof(TEntity).Name}:{query.Id}";
        var cachedDto = await _cacheService.GetAsync<TDto>(cacheKey);

        if (cachedDto != null)
        {
            return Result.Success(cachedDto);
        }

        var dto = await _dtoRepository.GetReadModelByIdAsync(query.Id, cancellationToken);

        if (dto is null)
        {
            return Result.Failure<TDto>(
                Error.NotFound($"No se encontró el registro de {typeof(TEntity).Name} con ID '{query.Id}'."));
        }

        await _cacheService.SetAsync(
            cacheKey,
            dto,
            absoluteExpiration: TimeSpan.FromMinutes(30));

        return Result.Success(dto);
    }
}
