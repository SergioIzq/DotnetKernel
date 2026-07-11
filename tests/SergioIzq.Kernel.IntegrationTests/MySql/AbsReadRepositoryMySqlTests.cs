using Xunit;

namespace SergioIzq.Kernel.IntegrationTests.MySql;

/// <summary>
/// AbsReadRepository construye SQL de MySQL a base de strings (joins, LIKE, DATE_FORMAT,
/// LIMIT/OFFSET, filtros dinámicos): la única forma honesta de probarlo es contra un MySQL
/// real. Estos tests se omiten automáticamente si Docker no está disponible (en CI corren siempre).
/// </summary>
[Collection("MySqlContainer")]
public class AbsReadRepositoryMySqlTests
{
    private readonly MySqlContainerFixture _db;
    private readonly GastoReadRepository _repo;

    public AbsReadRepositoryMySqlTests(MySqlContainerFixture db)
    {
        _db = db;
        _repo = new GastoReadRepository(new TestConnectionFactory(db.ConnectionString));
    }

    [DockerFact]
    public async Task GetReadModelById_DevuelveElDtoConElJoin()
    {
        var dto = await _repo.GetReadModelByIdAsync(_db.GastoSupermercado);

        Assert.NotNull(dto);
        Assert.Equal("Compra supermercado", dto.Descripcion);
        Assert.Equal(25.50m, dto.Importe);
        Assert.Equal("Comida", dto.ConceptoNombre);
        Assert.Equal(_db.UsuarioA, dto.UsuarioId);
    }

    [DockerFact]
    public async Task GetReadModelById_Inexistente_DevuelveNull()
    {
        var dto = await _repo.GetReadModelByIdAsync(Guid.NewGuid());

        Assert.Null(dto);
    }

    [DockerFact]
    public async Task GetReadModelById_JoinSinMatch_UsaElCoalesce()
    {
        var dto = await _repo.GetReadModelByIdAsync(_db.GastoCine);

        Assert.NotNull(dto);
        Assert.Equal(string.Empty, dto.ConceptoNombre);
    }

    [DockerFact]
    public async Task GetPagedReadModels_PaginaYCuentaBien()
    {
        var page1 = await _repo.GetPagedReadModelsAsync(page: 1, pageSize: 3);
        var page2 = await _repo.GetPagedReadModelsAsync(page: 2, pageSize: 3);

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.True(page1.HasNextPage);
        Assert.Single(page2.Items);
        Assert.False(page2.HasNextPage);
    }

    [DockerFact]
    public async Task GetPagedByUser_SoloDevuelveLosDelUsuario()
    {
        var result = await _repo.GetPagedReadModelsByUserAsync(_db.UsuarioA, page: 1, pageSize: 10);

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, dto => Assert.Equal(_db.UsuarioA, dto.UsuarioId));
    }

    [DockerFact]
    public async Task BusquedaDeTexto_EsCaseInsensitive_YBuscaEnElJoin()
    {
        // "COMIDA" solo existe como nombre del concepto (tabla joineada), en otra caja
        var result = await _repo.GetPagedReadModelsByUserAsync(
            _db.UsuarioA, page: 1, pageSize: 10, searchTerm: "COMIDA");

        var dto = Assert.Single(result.Items);
        Assert.Equal("Compra supermercado", dto.Descripcion);
    }

    [DockerFact]
    public async Task BusquedaNumerica_MatcheaElImporteExacto()
    {
        var result = await _repo.GetPagedReadModelsByUserAsync(
            _db.UsuarioA, page: 1, pageSize: 10, searchTerm: "60");

        var dto = Assert.Single(result.Items);
        Assert.Equal("Gasolina", dto.Descripcion);
    }

    [DockerFact]
    public async Task BusquedaPorFecha_MatcheaElDia()
    {
        var result = await _repo.GetPagedReadModelsByUserAsync(
            _db.UsuarioA, page: 1, pageSize: 10, searchTerm: "2026-02-10");

        var dto = Assert.Single(result.Items);
        Assert.Equal("Gasolina", dto.Descripcion);
    }

    [DockerFact]
    public async Task OrdenDinamico_RespetaColumnaYDireccion()
    {
        var asc = await _repo.GetPagedReadModelsByUserAsync(
            _db.UsuarioA, page: 1, pageSize: 10, sortColumn: "Importe", sortOrder: "asc");

        Assert.Equal(15.00m, asc.Items.First().Importe);
        Assert.Equal(60.00m, asc.Items.Last().Importe);
    }

    [DockerFact]
    public async Task OrdenConColumnaInvalida_CaeAlOrdenPorDefecto()
    {
        // defaultOrderBy: fecha DESC → el más reciente por fecha primero
        var result = await _repo.GetPagedReadModelsByUserAsync(
            _db.UsuarioA, page: 1, pageSize: 10, sortColumn: "NoExiste", sortOrder: "asc");

        Assert.Equal("Entradas de cine", result.Items.First().Descripcion);
    }

    [DockerFact]
    public async Task Autocomplete_ConFiltrosExtra_FiltraPorLaColumnaDelJoin()
    {
        // "Compra" matchea supermercado (Comida); el filtro extra restringe al concepto
        var results = await _repo.SearchForAutocompleteAsync(
            _db.UsuarioA, searchTerm: "compra", limit: 10,
            extraFilters: new Dictionary<string, object> { { "c.nombre", "Comida" } });

        var dto = Assert.Single(results);
        Assert.Equal("Compra supermercado", dto.Descripcion);
    }

    [DockerFact]
    public async Task Autocomplete_NoDevuelveResultadosDeOtrosUsuarios()
    {
        // "Compra online" es del usuario B: buscando como A no debe aparecer
        var results = await _repo.SearchForAutocompleteAsync(_db.UsuarioA, searchTerm: "online", limit: 10);

        Assert.Empty(results);
    }

    [DockerFact]
    public async Task GetRecent_OrdenaPorFechaCreacionDescYRespetaElLimite()
    {
        var results = (await _repo.GetRecentAsync(_db.UsuarioA, limit: 2)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("Entradas de cine", results[0].Descripcion); // fecha_creacion más reciente
        Assert.Equal("Gasolina", results[1].Descripcion);
    }

    [DockerFact]
    public async Task FiltrosDinamicos_RechazanNombresDeColumnaMaliciosos()
    {
        // El nombre de columna se interpola en el SQL: el guard debe rechazar
        // cualquier cosa que no sea un identificador (anti inyección)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repo.SearchForAutocompleteAsync(
                _db.UsuarioA, searchTerm: "compra", limit: 10,
                extraFilters: new Dictionary<string, object> { { "1=1; DROP TABLE gastos; --", "x" } }));
    }
}
