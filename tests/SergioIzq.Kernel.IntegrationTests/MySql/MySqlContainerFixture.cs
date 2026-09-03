using Dapper;
using SergioIzq.Infrastructure.Kernel.TypesHandlers;
using Testcontainers.MySql;
using Xunit;

namespace SergioIzq.Kernel.IntegrationTests.MySql;

/// <summary>
/// Arranca un MySQL 8.0 real en Docker (una vez para toda la colección), crea el esquema
/// con la misma forma que usa Kash (snake_case, ids BINARY(16)) y siembra datos conocidos.
/// </summary>
public sealed class MySqlContainerFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.0")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    // Datos sembrados, con ids/valores fijos para poder afirmar sobre ellos
    public Guid UsuarioA { get; } = Guid.NewGuid();
    public Guid UsuarioB { get; } = Guid.NewGuid();
    public Guid ConceptoComida { get; } = Guid.NewGuid();
    public Guid ConceptoTransporte { get; } = Guid.NewGuid();
    public Guid GastoSupermercado { get; } = Guid.NewGuid();
    public Guid GastoGasolina { get; } = Guid.NewGuid();
    public Guid GastoCine { get; } = Guid.NewGuid();
    public Guid GastoUsuarioB { get; } = Guid.NewGuid();

    // Tabla independiente de "gastos": para GetRecentAsync necesitamos un caso donde la
    // recencia (fecha_creacion) y el orden alfabético (defaultOrderBy) diverjan. Usar una
    // tabla propia evita alterar el recuento/orden de filas que ya afirman los tests de
    // "gastos" (paginación, búsqueda, orden dinámico).
    public Guid ProductoZapateria { get; } = Guid.NewGuid();
    public Guid ProductoAlimentacion { get; } = Guid.NewGuid();
    public Guid ProductoFerreteria { get; } = Guid.NewGuid();
    public Guid ProductoBebidas { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Misma configuración global de Dapper que usan las apps consumidoras
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        DapperTypeHandlerExtensions.RegisterGuidBinaryHandler();

        using var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        // CHAR(36): el formato en el que Pomelo/EF Core guarda los Guid por defecto (y el que
        // MySqlConnector envía para parámetros Guid de Dapper) — misma forma que las BDs reales.
        await connection.ExecuteAsync("""
            CREATE TABLE conceptos (
                id CHAR(36) PRIMARY KEY,
                nombre VARCHAR(100) NOT NULL
            );

            CREATE TABLE gastos (
                id CHAR(36) PRIMARY KEY,
                id_usuario CHAR(36) NOT NULL,
                id_concepto CHAR(36) NULL,
                descripcion VARCHAR(200) NOT NULL,
                importe DECIMAL(10,2) NOT NULL,
                fecha DATETIME NOT NULL,
                fecha_creacion DATETIME NOT NULL
            );

            CREATE TABLE productos_test (
                id CHAR(36) PRIMARY KEY,
                id_usuario CHAR(36) NOT NULL,
                nombre VARCHAR(100) NOT NULL,
                fecha_creacion DATETIME NOT NULL
            );
            """);

        await connection.ExecuteAsync(
            "INSERT INTO conceptos (id, nombre) VALUES (@Id, @Nombre)",
            new[]
            {
                new { Id = ConceptoComida, Nombre = "Comida" },
                new { Id = ConceptoTransporte, Nombre = "Transporte" }
            });

        await connection.ExecuteAsync(
            """
            INSERT INTO gastos (id, id_usuario, id_concepto, descripcion, importe, fecha, fecha_creacion)
            VALUES (@Id, @UsuarioId, @ConceptoId, @Descripcion, @Importe, @Fecha, @FechaCreacion)
            """,
            new object[]
            {
                new { Id = GastoSupermercado, UsuarioId = UsuarioA, ConceptoId = (Guid?)ConceptoComida, Descripcion = "Compra supermercado", Importe = 25.50m, Fecha = new DateTime(2026, 1, 15), FechaCreacion = new DateTime(2026, 1, 15, 10, 0, 0) },
                new { Id = GastoGasolina, UsuarioId = UsuarioA, ConceptoId = (Guid?)ConceptoTransporte, Descripcion = "Gasolina", Importe = 60.00m, Fecha = new DateTime(2026, 2, 10), FechaCreacion = new DateTime(2026, 2, 10, 10, 0, 0) },
                new { Id = GastoCine, UsuarioId = UsuarioA, ConceptoId = (Guid?)null, Descripcion = "Entradas de cine", Importe = 15.00m, Fecha = new DateTime(2026, 3, 5), FechaCreacion = new DateTime(2026, 3, 5, 10, 0, 0) },
                new { Id = GastoUsuarioB, UsuarioId = UsuarioB, ConceptoId = (Guid?)ConceptoComida, Descripcion = "Compra online", Importe = 99.99m, Fecha = new DateTime(2026, 2, 20), FechaCreacion = new DateTime(2026, 2, 20, 10, 0, 0) }
            });

        // Orden de creación (más antiguo → más reciente): Bebidas, Ferretería, Alimentación, Zapatería.
        // Con limit=2, "recientes" debe seleccionar {Zapatería, Alimentación} (excluyendo Ferretería
        // y Bebidas aunque las precedan alfabéticamente) y devolverlas en orden alfabético: Alimentación
        // antes que Zapatería — el inverso del orden de recencia con el que se seleccionaron.
        await connection.ExecuteAsync(
            "INSERT INTO productos_test (id, id_usuario, nombre, fecha_creacion) VALUES (@Id, @UsuarioId, @Nombre, @FechaCreacion)",
            new[]
            {
                new { Id = ProductoBebidas, UsuarioId = UsuarioA, Nombre = "Bebidas", FechaCreacion = new DateTime(2026, 1, 1) },
                new { Id = ProductoFerreteria, UsuarioId = UsuarioA, Nombre = "Ferretería", FechaCreacion = new DateTime(2026, 1, 2) },
                new { Id = ProductoAlimentacion, UsuarioId = UsuarioA, Nombre = "Alimentación", FechaCreacion = new DateTime(2026, 1, 3) },
                new { Id = ProductoZapateria, UsuarioId = UsuarioA, Nombre = "Zapatería", FechaCreacion = new DateTime(2026, 1, 4) }
            });
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("MySqlContainer")]
public class MySqlContainerCollection : ICollectionFixture<MySqlContainerFixture>
{
}
