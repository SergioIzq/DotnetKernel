using SergioIzq.Application.Kernel.Messaging.Abstracts.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Interfaces;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts;

public abstract class AbsQueryHandler<TEntity, TId> : IQueryHandlerBase<TEntity, TId>
    where TEntity : AbsEntity<TId>
    where TId : IGuidValueObject
{
    protected readonly ICacheService _cacheService;

    protected AbsQueryHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }
}
