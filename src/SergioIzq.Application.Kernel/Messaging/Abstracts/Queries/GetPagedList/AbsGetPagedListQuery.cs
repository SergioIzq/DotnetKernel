using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;

public abstract record AbsGetPagedListQuery<TEntity, TId, TDto>(
    int Page,
    int PageSize,
    string SearchTerm = "",
    string SortColumn = "",
    string SortOrder = "",
    Guid? UsuarioId = null) : IRequest<Result<PagedList<TDto>>>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
    where TDto : class;
