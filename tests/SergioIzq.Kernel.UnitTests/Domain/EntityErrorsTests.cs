using SergioIzq.Domain.Kernel.Abstractions.Enums;
using SergioIzq.Domain.Kernel.Abstractions.Errors;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Domain;

public class EntityErrorsTests
{
    [Fact]
    public void DuplicateName_EsConflictConLaFraseCompleta()
    {
        var error = EntityErrors.DuplicateName("una categoría", "Comida");

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("Ya existe una categoría con el nombre 'Comida'.", error.Message);
    }
}
