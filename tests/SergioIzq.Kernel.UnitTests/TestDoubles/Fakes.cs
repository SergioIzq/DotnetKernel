using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;

namespace SergioIzq.Kernel.UnitTests.TestDoubles;

/// <summary>
/// Caché en memoria que además registra cada operación (Get/Set/Remove) con su clave,
/// para poder afirmar la coherencia de claves entre command handlers y query handlers.
/// </summary>
public sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = [];

    public List<string> GetKeys { get; } = [];
    public List<string> SetKeys { get; } = [];
    public List<string> RemovedKeys { get; } = [];

    public Task<T?> GetAsync<T>(string key)
    {
        GetKeys.Add(key);
        if (_store.TryGetValue(key, out var value) && value is T typed)
        {
            return Task.FromResult<T?>(typed);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null)
    {
        SetKeys.Add(key);
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        RemovedKeys.Add(key);
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key) => Task.FromResult(_store.ContainsKey(key));

    public Task InvalidateByPatternAsync(string pattern) => Task.CompletedTask;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class FakeWriteRepository : IWriteRepository<TestEntity, TestId>
{
    public List<TestEntity> Added { get; } = [];
    public List<TestEntity> Updated { get; } = [];
    public List<TestEntity> Deleted { get; } = [];
    public TestEntity? EntityToReturn { get; set; }

    public Task<TestEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(EntityToReturn);

    public void Add(TestEntity entity) => Added.Add(entity);

    public Task CreateAsync(TestEntity entity, CancellationToken cancellationToken = default)
    {
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TestEntity entity) => Updated.Add(entity);
    public void Delete(TestEntity entity) => Deleted.Add(entity);
}

public sealed class FakeReadRepository : IReadRepository<TestEntity, TestDto, TestId>
{
    public TestDto? DtoToReturn { get; set; }
    public PagedList<TestDto> PagedToReturn { get; set; } = new([], 1, 10, 0);

    public Task<TestDto?> GetReadModelByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(DtoToReturn);

    public Task<IEnumerable<TestDto>> GetAllReadModelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Enumerable.Empty<TestDto>());

    public Task<PagedList<TestDto>> GetPagedReadModelsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(PagedToReturn);

    public Task<PagedList<TestDto>> GetPagedReadModelsByUserAsync(
        Guid usuarioId, int page, int pageSize,
        string? searchTerm = null, string? sortColumn = null, string? sortOrder = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(PagedToReturn);

    public Task<IEnumerable<TestDto>> SearchForAutocompleteAsync(
        Guid usuarioId, string searchTerm, int limit = 10,
        Dictionary<string, object>? extraFilters = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Enumerable.Empty<TestDto>());

    public Task<IEnumerable<TestDto>> GetRecentAsync(
        Guid usuarioId, int limit = 5,
        Dictionary<string, object>? extraFilters = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Enumerable.Empty<TestDto>());
}

public sealed class FakeUserContext : IUserContext
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
}
