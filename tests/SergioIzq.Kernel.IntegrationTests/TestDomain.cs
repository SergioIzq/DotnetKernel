using System.Reflection;
using MediatR;
using SergioIzq.Application.Kernel.Interfaces;
using SergioIzq.Application.Kernel.Services;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Events;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Infrastructure.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SergioIzq.Kernel.IntegrationTests;

// --- Dominio mínimo de prueba, como lo definiría una app consumidora ---

public readonly record struct PedidoId : IGuidValueObject
{
    public Guid Value { get; init; }

    private PedidoId(Guid value) { Value = value; }

    public static Result<PedidoId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result.Failure<PedidoId>(Error.Validation("El ID no puede estar vacío."));
        return Result.Success(new PedidoId(value));
    }

    public static PedidoId CreateFromDatabase(Guid value) => new(value);

    public static PedidoId New() => new(Guid.NewGuid());
}

public sealed record PedidoCreadoEvent(Guid PedidoId) : DomainEventBase;

public sealed class Pedido : AbsEntity<PedidoId>
{
    public string Descripcion { get; private set; }

    private Pedido() : base(default) { Descripcion = string.Empty; } // EF

    public Pedido(PedidoId id, string descripcion) : base(id)
    {
        Descripcion = descripcion;
        AddDomainEvent(new PedidoCreadoEvent(id.Value));
    }
}

public sealed class TestDbContext : KernelDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override Assembly DomainAssembly => typeof(Pedido).Assembly;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración mínima de los Ids (una app real usaría IEntityTypeConfiguration)
        modelBuilder.Entity<Pedido>(b =>
        {
            b.Property(e => e.Id).HasConversion(id => id.Value, value => PedidoId.CreateFromDatabase(value));
        });

        modelBuilder.Entity<CuentaTest>(b =>
        {
            b.Property(e => e.Id).HasConversion(id => id.Value, value => PedidoId.CreateFromDatabase(value));
        });

        modelBuilder.Entity<MovimientoTest>(b =>
        {
            b.Property(e => e.Id).HasConversion(id => id.Value, value => PedidoId.CreateFromDatabase(value));
        });

        // GastoEntity solo se usa en los tests de Dapper, pero el escaneo por convención
        // de KernelDbContext la registra igualmente (vive en este mismo assembly)
        modelBuilder.Entity<MySql.GastoEntity>(b =>
        {
            b.Property(e => e.Id).HasConversion(id => id.Value, value => PedidoId.CreateFromDatabase(value));
        });
    }
}

// --- Réplica del patrón de saldo de Kash: un movimiento cuyo handler actualiza otra entidad ---

public sealed record MovimientoCreadoEvent(Guid CuentaId, decimal Importe, bool GuardarDentroDelHandler) : DomainEventBase;

public sealed class CuentaTest : AbsEntity<PedidoId>
{
    public decimal Saldo { get; private set; }

    private CuentaTest() : base(default) { } // EF

    public CuentaTest(PedidoId id, decimal saldoInicial) : base(id)
    {
        Saldo = saldoInicial;
    }

    public void Retirar(decimal importe) => Saldo -= importe;
}

public sealed class MovimientoTest : AbsEntity<PedidoId>
{
    public Guid CuentaId { get; private set; }
    public decimal Importe { get; private set; }

    private MovimientoTest() : base(default) { } // EF

    public MovimientoTest(PedidoId id, Guid cuentaId, decimal importe, bool guardarDentroDelHandler) : base(id)
    {
        CuentaId = cuentaId;
        Importe = importe;
        AddDomainEvent(new MovimientoCreadoEvent(cuentaId, importe, guardarDentroDelHandler));
    }
}

/// <summary>
/// Réplica de los event handlers de saldo de Kash (GastoCreadoEventHandler /
/// IngresoCreadoEventHandler): carga la cuenta, resta el importe y, según el evento,
/// llama o no a SaveChangesAsync dentro del propio handler.
/// </summary>
public sealed class SaldoDeCuentaHandler : INotificationHandler<MovimientoCreadoEvent>
{
    public static int Ejecuciones;

    private readonly TestDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public SaldoDeCuentaHandler(TestDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(MovimientoCreadoEvent notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Ejecuciones);

        var cuenta = await _context.Set<CuentaTest>().SingleAsync(cancellationToken);
        cuenta.Retirar(notification.Importe);
        _context.Update(cuenta);

        if (notification.GuardarDentroDelHandler)
        {
            // Patrón de IngresoCreadoEventHandler: guardar dentro del handler.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        // Patrón de GastoCreadoEventHandler: no guardar; el dispatch pre-save
        // garantiza que el SaveChanges en curso persista también este cambio.
    }
}

/// <summary>Contador de eventos publicados: la pieza clave del test de dispatch único.</summary>
public sealed class PedidoCreadoContador : INotificationHandler<PedidoCreadoEvent>
{
    public static int Publicados;

    public Task Handle(PedidoCreadoEvent notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Publicados);
        return Task.CompletedTask;
    }
}

public sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null) => Task.CompletedTask;
    public Task RemoveAsync(string key) => Task.CompletedTask;
    public Task<bool> ExistsAsync(string key) => Task.FromResult(false);
    public Task InvalidateByPatternAsync(string pattern) => Task.CompletedTask;
}

public sealed class FixedUserContext : IUserContext
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
}
