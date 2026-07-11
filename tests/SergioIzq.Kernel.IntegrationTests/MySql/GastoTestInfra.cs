using System.Data;
using MySqlConnector;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Infrastructure.Kernel.Persistence;

namespace SergioIzq.Kernel.IntegrationTests.MySql;

// Dominio mínimo con la misma forma que usa Kash: tabla con snake_case, ids BINARY(16),
// join a una tabla relacionada, y columnas buscables/ordenables de texto, número y fecha.

public sealed class GastoEntity : AbsEntity<PedidoId>
{
    private GastoEntity() : base(default) { }
}

public sealed class GastoTestDto
{
    public Guid Id { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public DateTime Fecha { get; set; }
    public string ConceptoNombre { get; set; } = string.Empty;
    public Guid UsuarioId { get; set; }
}

public sealed class GastoReadRepository : AbsReadRepository<GastoEntity, GastoTestDto, PedidoId>
{
    public GastoReadRepository(IDbConnectionFactory dbConnectionFactory) : base(dbConnectionFactory)
    {
    }

    protected override ReadRepositoryConfiguration ConfigureRepository()
    {
        return ReadRepositoryConfiguration.WithJoins(
            tableName: "gastos",
            tableAlias: "g",
            selectColumns:
            [
                "g.id as Id",
                "g.descripcion as Descripcion",
                "g.importe as Importe",
                "g.fecha as Fecha",
                "COALESCE(c.nombre, '') as ConceptoNombre",
                "g.id_usuario as UsuarioId"
            ],
            joinClause: "LEFT JOIN conceptos c ON g.id_concepto = c.id",
            searchableColumns: ["g.descripcion", "c.nombre"],
            numericSearchableColumns: ["g.importe"],
            dateSearchableColumns: ["g.fecha"],
            sortableColumns: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Importe", "g.importe" },
                { "Fecha", "g.fecha" },
                { "ConceptoNombre", "c.nombre" }
            },
            defaultOrderBy: "g.fecha DESC, g.id DESC");
    }
}

public sealed class TestConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public TestConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new MySqlConnection(_connectionString);
}
