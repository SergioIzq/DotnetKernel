using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Interfaces;

public interface IAbsCommandHandlerBase<TEntity, TId>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
{
    Task<Result<Guid>> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<Result<Guid>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}
