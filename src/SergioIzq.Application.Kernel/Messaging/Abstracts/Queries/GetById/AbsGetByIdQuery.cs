using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;

public abstract record AbsGetByIdQuery<TEntity, TId, TDto>(Guid Id) : IRequest<Result<TDto>>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
    where TDto : class;
