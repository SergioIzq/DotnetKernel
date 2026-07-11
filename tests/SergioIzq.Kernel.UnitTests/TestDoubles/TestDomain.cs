using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Events;
using SergioIzq.Domain.Kernel.Interfaces;

namespace SergioIzq.Kernel.UnitTests.TestDoubles;

public readonly record struct TestId : IGuidValueObject
{
    public Guid Value { get; init; }

    private TestId(Guid value) { Value = value; }

    public static Result<TestId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result.Failure<TestId>(Error.Validation("El ID no puede estar vacío."));
        return Result.Success(new TestId(value));
    }

    public static TestId CreateFromDatabase(Guid value) => new(value);

    public static TestId New() => new(Guid.NewGuid());
}

public sealed record TestCreatedEvent(Guid EntityId) : DomainEventBase;

public sealed class TestEntity : AbsEntity<TestId>
{
    public string Nombre { get; private set; }

    public TestEntity(TestId id, string nombre) : base(id)
    {
        Nombre = nombre;
    }

    public void RaiseCreatedEvent() => AddDomainEvent(new TestCreatedEvent(Id.Value));
}

public sealed class TestDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
