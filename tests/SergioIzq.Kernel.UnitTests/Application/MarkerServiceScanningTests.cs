using MediatR;
using SergioIzq.Application.Kernel.DependencyInjection;
using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Application;

// --- Tipos de prueba para el escaneo (deben ser públicos y no anidados) ---

public interface IScopedThing { }
public class ScopedThing : IScopedThing, IApplicationService { }

public interface ITransientThing { }
public class TransientThing : ITransientThing, ITransientService { }

public interface ISingletonThing { }
public class SingletonThing : ISingletonThing, ISingletonService { }

// Handler de MediatR marcado: el escáner debe ignorarlo por completo
// (los handlers los registra AddMediatR, no el escaneo por marcador).
public sealed record DummyRequest : IRequest<Result>;
public interface IAlsoAService { }
public class DummyRequestHandler : IRequestHandler<DummyRequest, Result>, IApplicationService, IAlsoAService
{
    public Task<Result> Handle(DummyRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

// Sin marcador: no debe registrarse aunque tenga interfaces
public interface INotMarked { }
public class NotMarkedThing : INotMarked { }

public class MarkerServiceScanningTests
{
    private static ServiceCollection Scan()
    {
        var services = new ServiceCollection();
        services.AddMarkedServices(typeof(MarkerServiceScanningTests).Assembly);
        return services;
    }

    [Theory]
    [InlineData(typeof(IScopedThing), typeof(ScopedThing), ServiceLifetime.Scoped)]
    [InlineData(typeof(ITransientThing), typeof(TransientThing), ServiceLifetime.Transient)]
    [InlineData(typeof(ISingletonThing), typeof(SingletonThing), ServiceLifetime.Singleton)]
    public void MarcadoresDeterminanElLifetime(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        var services = Scan();

        var descriptor = Assert.Single(services, d => d.ServiceType == serviceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void HandlersDeMediatR_SeExcluyenAunqueTenganMarcador()
    {
        var services = Scan();

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(DummyRequestHandler));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IAlsoAService));
    }

    [Fact]
    public void ClasesSinMarcador_NoSeRegistran()
    {
        var services = Scan();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(INotMarked));
    }

    [Fact]
    public void LosMarcadoresEnSi_NoSeRegistranComoServicio()
    {
        var services = Scan();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IApplicationService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ITransientService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISingletonService));
    }
}
