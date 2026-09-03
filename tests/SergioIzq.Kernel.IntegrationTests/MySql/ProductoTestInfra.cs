using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Infrastructure.Kernel.Persistence;

namespace SergioIzq.Kernel.IntegrationTests.MySql;

// Dominio mínimo con la misma forma que usan los catálogos de Kash: tabla simple sin
// joins, id/nombre/id_usuario/fecha_creacion, orden por defecto alfabético.

public sealed class ProductoEntity : AbsEntity<PedidoId>
{
    private ProductoEntity() : base(default) { }
}

public sealed class ProductoTestDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public Guid UsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public sealed class ProductoReadRepository : AbsReadRepository<ProductoEntity, ProductoTestDto, PedidoId>
{
    public ProductoReadRepository(IDbConnectionFactory dbConnectionFactory) : base(dbConnectionFactory)
    {
    }

    protected override ReadRepositoryConfiguration ConfigureRepository()
    {
        return ReadRepositoryConfiguration.Simple(
            tableName: "productos_test",
            selectColumns:
            [
                "id as Id",
                "nombre as Nombre",
                "id_usuario as UsuarioId",
                "fecha_creacion as FechaCreacion"
            ],
            searchableColumns: ["nombre"],
            sortableColumns: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Nombre", "nombre" },
                { "FechaCreacion", "fecha_creacion" }
            },
            defaultOrderBy: "nombre ASC");
    }
}
