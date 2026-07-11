using Mapster;
using SergioIzq.Application.Kernel.Mapping;
using SergioIzq.Kernel.UnitTests.TestDoubles;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Application;

public class MapsterGuidValueObjectTests
{
    [Fact]
    public void RegisterGuidValueObjects_MapeaAmbasDirecciones()
    {
        var config = new TypeAdapterConfig();
        config.RegisterGuidValueObjects(typeof(TestId).Assembly);

        var guid = Guid.NewGuid();

        // Guid -> Id (vía CreateFromDatabase, la dirección que antes había que registrar a mano)
        var id = guid.Adapt<TestId>(config);
        Assert.Equal(guid, id.Value);

        // Id -> Guid (vía Value)
        var roundtrip = id.Adapt<Guid>(config);
        Assert.Equal(guid, roundtrip);
    }

    [Fact]
    public void RegisterGuidValueObjects_MapeaPropiedadesAnidadas()
    {
        var config = new TypeAdapterConfig();
        config.RegisterGuidValueObjects(typeof(TestId).Assembly);

        var entity = new TestEntity(TestId.New(), "hola");

        var dto = entity.Adapt<TestDto>(config);

        Assert.Equal(entity.Id.Value, dto.Id);
        Assert.Equal("hola", dto.Nombre);
    }
}
