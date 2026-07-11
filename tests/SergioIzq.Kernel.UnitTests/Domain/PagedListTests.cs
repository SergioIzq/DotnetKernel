using SergioIzq.Domain.Kernel.Abstractions.Results;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Domain;

public class PagedListTests
{
    [Theory]
    [InlineData(1, 10, 25, true, false)]  // primera página de tres
    [InlineData(2, 10, 25, true, true)]   // página intermedia
    [InlineData(3, 10, 25, false, true)]  // última página
    [InlineData(1, 10, 5, false, false)]  // única página
    [InlineData(1, 10, 0, false, false)]  // vacío
    public void Paginacion_CalculaHasNextYHasPrevious(int page, int pageSize, int total, bool hasNext, bool hasPrevious)
    {
        var list = new PagedList<int>([], page, pageSize, total);

        Assert.Equal(hasNext, list.HasNextPage);
        Assert.Equal(hasPrevious, list.HasPreviousPage);
    }
}
