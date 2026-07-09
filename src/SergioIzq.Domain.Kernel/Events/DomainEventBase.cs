using SergioIzq.Domain.Kernel.Interfaces;

namespace SergioIzq.Domain.Kernel.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; init; }
    public DateTime OcurredOn { get; init; }

    protected DomainEventBase()
    {
        EventId = Guid.NewGuid();
        OcurredOn = DateTime.UtcNow;
    }
}
