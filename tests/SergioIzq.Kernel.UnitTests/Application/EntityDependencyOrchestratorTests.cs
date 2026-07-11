using SergioIzq.Application.Kernel.Orchestration;
using SergioIzq.Domain.Kernel.Abstractions.Enums;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Application;

public class EntityDependencyOrchestratorTests
{
    private static DependencyStep Step(
        string key,
        Guid? resolves,
        bool required = true,
        Func<IReadOnlyDictionary<string, Guid>, Dictionary<string, object>>? additionalData = null)
    {
        return new DependencyStep(
            Key: key,
            Id: null,
            Nombre: key,
            FindOrCreateAsync: (_, _, _, _, _) => Task.FromResult(resolves),
            ToDependencyValue: guid => guid,
            Required: required,
            AdditionalData: additionalData);
    }

    [Fact]
    public async Task ResuelveTodosLosPasos_YDevuelveElDiccionario()
    {
        var orchestrator = new EntityDependencyOrchestrator();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var result = await orchestrator.ResolveAsync(Guid.NewGuid(),
        [
            Step("A", idA),
            Step("B", idB)
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(idA, result.Value["A"]);
        Assert.Equal(idB, result.Value["B"]);
    }

    [Fact]
    public async Task PasoRequeridoSinResultado_FallaConValidacion()
    {
        var orchestrator = new EntityDependencyOrchestrator();

        var result = await orchestrator.ResolveAsync(Guid.NewGuid(),
        [
            Step("A", resolves: null, required: true)
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("'A'", result.Error.Message);
    }

    [Fact]
    public async Task PasoOpcionalSinResultado_SeOmiteYContinua()
    {
        var orchestrator = new EntityDependencyOrchestrator();
        var idB = Guid.NewGuid();

        var result = await orchestrator.ResolveAsync(Guid.NewGuid(),
        [
            Step("A", resolves: null, required: false),
            Step("B", idB)
        ]);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ContainsKey("A"));
        Assert.Equal(idB, result.Value["B"]);
    }

    [Fact]
    public async Task LosGuidsResueltos_SePropaganComoAdditionalDataDePasosSiguientes()
    {
        var orchestrator = new EntityDependencyOrchestrator();
        var idA = Guid.NewGuid();
        Dictionary<string, object>? recibidoEnB = null;

        var stepB = new DependencyStep(
            Key: "B",
            Id: null,
            Nombre: "B",
            FindOrCreateAsync: (_, _, _, additionalData, _) =>
            {
                recibidoEnB = additionalData;
                return Task.FromResult<Guid?>(Guid.NewGuid());
            },
            ToDependencyValue: guid => guid,
            AdditionalData: resueltos => new Dictionary<string, object> { { "AId", resueltos["A"] } });

        var result = await orchestrator.ResolveAsync(Guid.NewGuid(), [Step("A", idA), stepB]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(recibidoEnB);
        Assert.Equal(idA, recibidoEnB!["AId"]);
    }
}
