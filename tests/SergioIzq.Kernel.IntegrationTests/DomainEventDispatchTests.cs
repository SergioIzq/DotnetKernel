using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Infrastructure.Kernel.DependencyInjection;
using SergioIzq.Infrastructure.Kernel.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SergioIzq.Kernel.IntegrationTests;

public class DomainEventDispatchTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DomainEventDispatchTests).Assembly));
        services.AddKernelUnitOfWork();

        services.AddDbContext<TestDbContext>((sp, options) =>
            options.UseInMemoryDatabase(dbName)
                   .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                   .AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor>()));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TestDbContext>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GuardarEntidadConEvento_LoPublicaExactamenteUnaVez()
    {
        // Regresión del bug encontrado en Kash: UnitOfWork e interceptor despachaban
        // cada evento de dominio dos veces (y el de UnitOfWork antes de confirmar el guardado).
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        PedidoCreadoContador.Publicados = 0;

        context.Set<Pedido>().Add(new Pedido(PedidoId.New(), "pedido de prueba"));
        await uow.SaveChangesAsync();

        Assert.Equal(1, PedidoCreadoContador.Publicados);
    }

    [Fact]
    public async Task GuardarSinEventos_NoPublicaNada()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pedido = new Pedido(PedidoId.New(), "pedido");
        pedido.ClearDomainEvents();

        PedidoCreadoContador.Publicados = 0;

        context.Set<Pedido>().Add(pedido);
        await uow.SaveChangesAsync();

        Assert.Equal(0, PedidoCreadoContador.Publicados);
    }
}
