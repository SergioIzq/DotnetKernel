using SergioIzq.Infrastructure.Kernel.Scheduling;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Infrastructure;

public class CronExpressionConverterTests
{
    private static readonly DateTime Fecha = new(2026, 3, 15, 14, 30, 0); // domingo 15/03 14:30

    [Theory]
    [InlineData("diaria", "30 14 * * *")]
    [InlineData("daily", "30 14 * * *")]
    [InlineData("semanal", "30 14 * * 0")] // domingo = 0
    [InlineData("weekly", "30 14 * * 0")]
    [InlineData("mensual", "30 14 15 * *")]
    [InlineData("monthly", "30 14 15 * *")]
    [InlineData("anual", "30 14 15 3 *")]
    [InlineData("yearly", "30 14 15 3 *")]
    [InlineData("ANUAL", "30 14 15 3 *")] // case-insensitive
    public void ConvierteCadaFrecuenciaASuCron(string frecuencia, string expected)
    {
        Assert.Equal(expected, CronExpressionConverter.ConvertirFrecuenciaACron(frecuencia, Fecha));
    }

    [Fact]
    public void FrecuenciaDesconocida_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            CronExpressionConverter.ConvertirFrecuenciaACron("quincenal", Fecha));
    }
}
