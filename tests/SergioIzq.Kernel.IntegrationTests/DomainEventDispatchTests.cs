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

    [Fact]
    public async Task HandlerQueModificaOtraEntidad_PersisteElCambioEnElMismoGuardado()
    {
        // Regresión del bug de Kash tras la migración a 0.2.x: GastoCreadoEventHandler
        // actualiza el saldo de la cuenta SIN llamar a SaveChanges, confiando en que el
        // dispatch ocurre ANTES del guardado y el mismo SaveChanges persiste ambas cosas.
        // Con dispatch post-save (interceptor) el saldo se perdía silenciosamente.
        var dbName = Guid.NewGuid().ToString();
        using var provider = BuildProvider(dbName);

        var cuentaId = PedidoId.New();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            context.Set<CuentaTest>().Add(new CuentaTest(cuentaId, 100m));
            await uow.SaveChangesAsync();

            SaldoDeCuentaHandler.Ejecuciones = 0;

            context.Set<MovimientoTest>().Add(
                new MovimientoTest(PedidoId.New(), cuentaId.Value, 30m, guardarDentroDelHandler: false));
            await uow.SaveChangesAsync();
        }

        Assert.Equal(1, SaldoDeCuentaHandler.Ejecuciones);

        // Verificación en scope nuevo: el cambio del handler tiene que estar en la BD.
        using var verifyScope = provider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var cuenta = await verifyContext.Set<CuentaTest>().SingleAsync();
        Assert.Equal(70m, cuenta.Saldo);
    }

    [Fact]
    public async Task HandlerQueGuardaDentroDelHandler_TerminaYPublicaUnaSolaVez()
    {
        // Regresión del timeout de Kash: IngresoCreadoEventHandler llama a SaveChangesAsync
        // DENTRO del handler. Con dispatch post-save y limpieza de eventos tardía, cada
        // guardado re-disparaba el interceptor con los eventos aún presentes → recursión
        // infinita → peticiones colgadas. Con dispatch pre-save (clear antes de publicar)
        // la re-entrada no encuentra eventos y termina.
        var dbName = Guid.NewGuid().ToString();
        using var provider = BuildProvider(dbName);

        var cuentaId = PedidoId.New();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            context.Set<CuentaTest>().Add(new CuentaTest(cuentaId, 100m));
            await uow.SaveChangesAsync();

            SaldoDeCuentaHandler.Ejecuciones = 0;

            context.Set<MovimientoTest>().Add(
                new MovimientoTest(PedidoId.New(), cuentaId.Value, 25m, guardarDentroDelHandler: true));

            // Guardia anti-regresión: si volviera la recursión infinita, el test falla a
            // los 20s en lugar de colgar la suite entera.
            var saveTask = uow.SaveChangesAsync();
            var completed = await Task.WhenAny(saveTask, Task.Delay(TimeSpan.FromSeconds(20)));
            Assert.Same(saveTask, completed);
            await saveTask;
        }

        Assert.Equal(1, SaldoDeCuentaHandler.Ejecuciones);

        using var verifyScope = provider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var cuenta = await verifyContext.Set<CuentaTest>().SingleAsync();
        Assert.Equal(75m, cuenta.Saldo);
    }
}
