using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;

public abstract class SearchForAutocompleteQueryHandler<TEntity, TDto, TQuery, TId>
    : AbsQueryHandler<TEntity, TId>, IRequestHandler<TQuery, Result<IEnumerable<TDto>>>
    where TEntity : AbsEntity<TId>
    where TQuery : SearchForAutocompleteQuery<TEntity, TDto, TId>
    where TDto : class
    where TId : IGuidValueObject
{
    protected readonly IReadRepository<TEntity, TDto, TId> _repository;

    protected SearchForAutocompleteQueryHandler(
        IReadRepository<TEntity, TDto, TId> repository,
        ICacheService cacheService)
        : base(cacheService)
    {
        _repository = repository;
    }

    protected virtual Dictionary<string, object>? GetCustomFilters(TQuery query) => null;

    public virtual async Task<Result<IEnumerable<TDto>>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            return Result.Success(Enumerable.Empty<TDto>());
        }

        if (!query.UsuarioId.HasValue)
        {
            return Result.Failure<IEnumerable<TDto>>(
                Error.Validation("El ID de usuario es requerido para la búsqueda."));
        }

        var extraFilters = GetCustomFilters(query);

        var results = await _repository.SearchForAutocompleteAsync(
            query.UsuarioId.Value,
            query.SearchTerm,
            query.Limit,
            extraFilters,
            cancellationToken);

        return Result.Success(results);
    }
}
