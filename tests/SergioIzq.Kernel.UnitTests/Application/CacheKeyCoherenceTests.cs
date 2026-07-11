using SergioIzq.Kernel.UnitTests.TestDoubles;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Application;

/// <summary>
/// El formato de las claves de caché es lógica load-bearing compartida entre los command
/// handlers (que invalidan) y los query handlers (que cachean): si se desincronizan, la app
/// sirve datos obsoletos en silencio. Estos tests fijan ese contrato.
/// </summary>
public class CacheKeyCoherenceTests
{
    [Fact]
    public async Task LaClaveQueCacheaGetById_EsLaMismaQueInvalidaElCommand()
    {
        var cache = new FakeCacheService();
        var userContext = new FakeUserContext();
        var entityId = Guid.NewGuid();

        // 1. El query handler cachea el DTO bajo su clave
        var readRepo = new FakeReadRepository { DtoToReturn = new TestDto { Id = entityId } };
        var queryHandler = new GetTestByIdQueryHandler(readRepo, cache);
        await queryHandler.Handle(new GetTestByIdQuery(entityId), CancellationToken.None);

        var cachedKey = Assert.Single(cache.SetKeys);

        // 2. El command handler actualiza la misma entidad e invalida
        var writeRepo = new FakeWriteRepository();
        var commandHandler = new CreateTestCommandHandler(new FakeUnitOfWork(), writeRepo, cache, userContext);
        var entity = new TestEntity(TestId.CreateFromDatabase(entityId), "x");
        await commandHandler.UpdateAsync(entity);

        // 3. La clave individual invalidada debe coincidir exactamente con la cacheada
        Assert.Contains(cachedKey, cache.RemovedKeys);
    }

    [Fact]
    public async Task LaVersionDeListaQueCreaGetPaged_EsLaMismaQueInvalidaElCommand()
    {
        var cache = new FakeCacheService();
        var userId = Guid.NewGuid();
        var userContext = new FakeUserContext { UserId = userId };

        // 1. La query paginada crea la clave de versión de lista para ese usuario
        var readRepo = new FakeReadRepository();
        var pagedHandler = new GetTestPagedQueryHandler(readRepo, cache);
        await pagedHandler.Handle(new GetTestPagedQuery(1, 10, userId), CancellationToken.None);

        var versionKey = Assert.Single(cache.SetKeys, k => k.StartsWith("list_version:"));

        // 2. Cualquier operación de escritura del mismo usuario debe invalidar exactamente esa clave
        var commandHandler = new CreateTestCommandHandler(new FakeUnitOfWork(), new FakeWriteRepository(), cache, userContext);
        await commandHandler.CreateAsync(new TestEntity(TestId.New(), "x"));

        Assert.Contains(versionKey, cache.RemovedKeys);
    }

    [Fact]
    public async Task GetById_SegundaLlamada_SirveDesdeCache()
    {
        var cache = new FakeCacheService();
        var entityId = Guid.NewGuid();
        var readRepo = new FakeReadRepository { DtoToReturn = new TestDto { Id = entityId } };
        var handler = new GetTestByIdQueryHandler(readRepo, cache);

        await handler.Handle(new GetTestByIdQuery(entityId), CancellationToken.None);

        // Si el repo devolviera null ahora, la segunda llamada solo puede tener éxito desde caché
        readRepo.DtoToReturn = null;
        var second = await handler.Handle(new GetTestByIdQuery(entityId), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(entityId, second.Value.Id);
    }

    [Fact]
    public async Task GetById_SinResultado_DevuelveNotFound()
    {
        var cache = new FakeCacheService();
        var handler = new GetTestByIdQueryHandler(new FakeReadRepository { DtoToReturn = null }, cache);

        var result = await handler.Handle(new GetTestByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SergioIzq.Domain.Kernel.Abstractions.Enums.ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task CreateHandler_FlujoCompleto_PersisteYDevuelveElId()
    {
        var cache = new FakeCacheService();
        var writeRepo = new FakeWriteRepository();
        var uow = new FakeUnitOfWork();
        var handler = new CreateTestCommandHandler(uow, writeRepo, cache, new FakeUserContext());

        var result = await handler.Handle(new CreateTestCommand("nueva"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(writeRepo.Added);
        Assert.Equal(added.Id.Value, result.Value);
        Assert.Equal(1, uow.SaveChangesCalls);
    }
}
