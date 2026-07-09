using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

/// <summary>
/// Comando base genérico para operaciones de creación.
/// </summary>
/// <typeparam name="TEntity">La entidad de dominio que se va a crear.</typeparam>
/// <typeparam name="TId">El tipo del Id de la entidad.</typeparam>
public abstract record AbsCreateCommand<TEntity, TId> : IRequest<Result<Guid>>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
{
}
