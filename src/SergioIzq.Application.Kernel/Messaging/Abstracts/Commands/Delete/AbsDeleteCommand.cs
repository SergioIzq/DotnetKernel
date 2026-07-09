using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

/// <summary>
/// Comando base genérico para operaciones de eliminación.
/// </summary>
/// <typeparam name="TEntity">La entidad de dominio que se va a eliminar.</typeparam>
/// <typeparam name="TId">El tipo del Id de la entidad.</typeparam>
public abstract record AbsDeleteCommand<TEntity, TId>(Guid Id) : IRequest<Result>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject;
