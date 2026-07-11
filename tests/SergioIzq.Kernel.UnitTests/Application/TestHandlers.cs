using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;
using SergioIzq.Application.Kernel.Messaging.Abstracts.Queries;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using SergioIzq.Kernel.UnitTests.TestDoubles;

namespace SergioIzq.Kernel.UnitTests.Application;

// Comandos/queries concretos mínimos sobre las familias abstractas del kernel,
// exactamente como los definiría una app consumidora.

public sealed record CreateTestCommand(string Nombre) : AbsCreateCommand<TestEntity, TestId>;

public sealed class CreateTestCommandHandler : AbsCreateCommandHandler<TestEntity, TestId, CreateTestCommand>
{
    public CreateTestCommandHandler(IUnitOfWork uow, IWriteRepository<TestEntity, TestId> repo, ICacheService cache, IUserContext user)
        : base(uow, repo, cache, user)
    {
    }

    protected override TestEntity CreateEntity(CreateTestCommand command, Dictionary<string, object>? dependencies = null)
        => new(TestId.New(), command.Nombre);
}

public sealed record GetTestByIdQuery(Guid Id) : AbsGetByIdQuery<TestEntity, TestId, TestDto>(Id);

public sealed class GetTestByIdQueryHandler : GetByIdQueryHandler<TestEntity, TestId, TestDto, GetTestByIdQuery>
{
    public GetTestByIdQueryHandler(IReadRepository<TestEntity, TestDto, TestId> repo, ICacheService cache)
        : base(repo, cache)
    {
    }
}

public sealed record GetTestPagedQuery(int Page, int PageSize, Guid? UsuarioId)
    : AbsGetPagedListQuery<TestEntity, TestId, TestDto>(Page, PageSize, UsuarioId: UsuarioId);

public sealed class GetTestPagedQueryHandler : GetPagedListQueryHandler<TestEntity, TestId, TestDto, GetTestPagedQuery>
{
    public GetTestPagedQueryHandler(IReadRepository<TestEntity, TestDto, TestId> repo, ICacheService cache)
        : base(repo, cache)
    {
    }
}
