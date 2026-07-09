using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Interfaces;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Interfaces;

/// <summary>
/// Interfaz marcadora, sin miembros: identifica a los query handlers construidos
/// sobre <see cref="AbsQueryHandler{TEntity, TId}"/> para una entidad concreta.
/// </summary>
public interface IQueryHandlerBase<TEntity, TId>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
{
}
