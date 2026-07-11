using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SergioIzq.Kernel.IntegrationTests;

public class KernelDbContextConventionTests
{
    private static TestDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public void LasEntidadesDelDomainAssembly_SeRegistranPorConvencion()
    {
        // El check original de Kash (IsSubclassOf de un genérico cerrado) era código muerto;
        // este test fija que el walk corregido de AbsEntity<> sí encuentra las entidades.
        using var context = BuildContext();

        var entityType = context.Model.FindEntityType(typeof(Pedido));

        Assert.NotNull(entityType);
    }

    [Fact]
    public void ElModelo_TieneIndiceUnicoSobreId()
    {
        using var context = BuildContext();

        var entityType = context.Model.FindEntityType(typeof(Pedido))!;
        var index = entityType.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == "Id"));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void ElCampoDeEventosDeDominio_NoSeMapea()
    {
        using var context = BuildContext();

        var entityType = context.Model.FindEntityType(typeof(Pedido))!;

        Assert.Null(entityType.FindProperty("_domainEvents"));
        Assert.Null(entityType.FindProperty("DomainEvents"));
    }
}
