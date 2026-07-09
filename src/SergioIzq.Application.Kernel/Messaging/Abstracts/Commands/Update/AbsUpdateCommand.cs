using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

/// <summary>
/// Comando base genérico para operaciones de actualización.
/// </summary>
/// <typeparam name="TEntity">La entidad de dominio que se va a actualizar.</typeparam>
/// <typeparam name="TId">El tipo del Id de la entidad.</typeparam>
/// <typeparam name="TDto">DTO asociado al comando (no se devuelve, solo mantiene el contrato del handler).</typeparam>
public abstract record AbsUpdateCommand<TEntity, TId, TDto> : IRequest<Result<Guid>>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
{
    public Guid Id { get; init; }
}
