using SergioIzq.Domain.Kernel.Abstractions;

namespace SergioIzq.Domain.Kernel.Interfaces;

public interface IDomainValidator
{
    Task<bool> ExistsAsync<TEntity, TId>(TId id)
        where TEntity : AbsEntity<TId>
        where TId : IGuidValueObject;
}
