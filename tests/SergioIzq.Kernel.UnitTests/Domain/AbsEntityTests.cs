using SergioIzq.Kernel.UnitTests.TestDoubles;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Domain;

public class AbsEntityTests
{
    [Fact]
    public void SinEventos_DomainEventsEstaVacio()
    {
        var entity = new TestEntity(TestId.New(), "a");

        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void AddDomainEvent_AcumulaEventos()
    {
        var entity = new TestEntity(TestId.New(), "a");

        entity.RaiseCreatedEvent();
        entity.RaiseCreatedEvent();

        Assert.Equal(2, entity.DomainEvents.Count);
    }

    [Fact]
    public void ClearDomainEvents_VaciaLaLista()
    {
        var entity = new TestEntity(TestId.New(), "a");
        entity.RaiseCreatedEvent();

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }
}
