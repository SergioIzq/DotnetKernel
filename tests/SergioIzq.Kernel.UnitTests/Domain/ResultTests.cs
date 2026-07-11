using SergioIzq.Domain.Kernel.Abstractions.Enums;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_NoTieneError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ExponeElError()
    {
        var error = Error.NotFound("no existe");
        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SuccessGenerico_ExponeElValor()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_EnFallo_Lanza()
    {
        var result = Result.Failure<int>(Error.Validation("inválido"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Create_ConNull_EsFalloNullValue()
    {
        var result = Result.Create<string>(null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NullValue, result.Error);
    }

    [Fact]
    public void ConversionImplicita_DesdeValor_EsSuccess()
    {
        Result<string> result = "hola";

        Assert.True(result.IsSuccess);
        Assert.Equal("hola", result.Value);
    }

    [Theory]
    [InlineData(nameof(Error.NotFound), ErrorType.NotFound)]
    [InlineData(nameof(Error.Conflict), ErrorType.Conflict)]
    [InlineData(nameof(Error.Validation), ErrorType.Validation)]
    [InlineData(nameof(Error.Unauthorized), ErrorType.Unauthorized)]
    [InlineData(nameof(Error.Forbidden), ErrorType.Forbidden)]
    public void FactoriasDeError_MapeanAlTipoCorrecto(string factory, ErrorType expected)
    {
        Error error = factory switch
        {
            nameof(Error.NotFound) => Error.NotFound(),
            nameof(Error.Conflict) => Error.Conflict(),
            nameof(Error.Validation) => Error.Validation(),
            nameof(Error.Unauthorized) => Error.Unauthorized(),
            nameof(Error.Forbidden) => Error.Forbidden(),
            _ => throw new ArgumentOutOfRangeException(nameof(factory))
        };

        Assert.Equal(expected, error.Type);
    }
}
