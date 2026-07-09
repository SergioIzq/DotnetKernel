using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using SergioIzq.Domain.Kernel.Interfaces;
using SergioIzq.Domain.Kernel.Interfaces.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

/// <summary>
/// Repositorio de lectura base implementado con Dapper: usa DTOs directamente desde SQL,
/// sin mapeo intermedio, y configuración declarativa (<see cref="ReadRepositoryConfiguration"/>)
/// en vez de tener que sobrescribir cada query.
///
/// Asume sintaxis SQL de MySQL (<c>LIMIT ... OFFSET</c>, <c>DATE_FORMAT</c>) — es la única
/// pieza del kernel acoplada a un motor de base de datos concreto, a propósito.
/// </summary>
/// <typeparam name="T">La entidad de dominio (hereda de <see cref="AbsEntity{TId}"/>).</typeparam>
/// <typeparam name="TReadModel">El DTO de lectura que Dapper mapea directamente desde SQL.</typeparam>
/// <typeparam name="TId">El tipo del Id de la entidad.</typeparam>
public abstract class AbsReadRepository<T, TReadModel, TId> : IReadRepository<T, TReadModel, TId>
    where T : AbsEntity<TId>
    where TReadModel : class
    where TId : IGuidValueObject
{
    protected readonly IDbConnectionFactory _dbConnectionFactory;
    protected readonly ReadRepositoryConfiguration _config;
    private readonly IDistributedCache? _cache;

    private static readonly Regex ValidColumnName =
        new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

    protected AbsReadRepository(IDbConnectionFactory dbConnectionFactory, IDistributedCache? cache = null)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _cache = cache;
        _config = ConfigureRepository();
    }

    /// <summary>
    /// Único método que debe sobrescribirse: describe tabla, columnas, joins y filtros.
    /// </summary>
    protected abstract ReadRepositoryConfiguration ConfigureRepository();

    #region Query builders (internos, usan _config)

    private string BuildSelectClause()
    {
        if (_config.SelectColumns == null || _config.SelectColumns.Count == 0)
        {
            return "id as Id,\n    id_usuario as UsuarioId,\n    fecha_creacion as FechaCreacion";
        }

        return string.Join(",\n    ", _config.SelectColumns);
    }

    private string BuildTableReference()
    {
        return string.IsNullOrEmpty(_config.TableAlias)
            ? _config.TableName
            : $"{_config.TableName} {_config.TableAlias}";
    }

    private string BuildFromClause()
    {
        var fromClause = $"FROM {BuildTableReference()}";

        if (!string.IsNullOrEmpty(_config.JoinClause))
        {
            fromClause += $"\n{_config.JoinClause}";
        }

        return fromClause;
    }

    private string BuildGetByIdQuery()
    {
        var tablePrefix = GetTablePrefix();
        return $"SELECT\n{BuildSelectClause()}\n{BuildFromClause()}\nWHERE {tablePrefix}id = @id";
    }

    private string BuildGetAllQuery() => $"SELECT\n{BuildSelectClause()}\n{BuildFromClause()}";

    private string BuildGetPagedQuery() => BuildGetAllQuery();

    private string BuildCountQuery() => $"SELECT COUNT(*) {BuildFromClause()}";

    private string GetTablePrefix() =>
        string.IsNullOrEmpty(_config.TableAlias) ? string.Empty : $"{_config.TableAlias}.";

    private string GetUserIdColumn() =>
        !string.IsNullOrEmpty(_config.UserIdColumn) ? _config.UserIdColumn : $"{GetTablePrefix()}id_usuario";

    private string GetDefaultOrderBy()
    {
        if (!string.IsNullOrEmpty(_config.DefaultOrderBy))
        {
            return $"ORDER BY {_config.DefaultOrderBy}";
        }

        return $"ORDER BY {GetTablePrefix()}fecha_creacion DESC";
    }

    private Dictionary<string, string> GetSortableColumns()
    {
        if (_config.SortableColumns != null && _config.SortableColumns.Count > 0)
        {
            return _config.SortableColumns;
        }

        var tablePrefix = GetTablePrefix();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FechaCreacion", $"{tablePrefix}fecha_creacion" },
            { "Fecha", $"{tablePrefix}fecha_creacion" }
        };
    }

    private List<string> GetSearchableColumns() => _config.SearchableColumns ?? [];

    private List<string> GetNumericSearchableColumns() => _config.NumericSearchableColumns ?? [];

    private List<string> GetDateSearchableColumns() => _config.DateSearchableColumns ?? [];

    /// <summary>
    /// Construye el WHERE de búsqueda para texto, números y fechas. Excluye automáticamente
    /// columnas que contengan "id" de la búsqueda de texto, para no buscar dentro de GUIDs.
    /// </summary>
    private string BuildSearchWhereClause(string searchTerm, DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return string.Empty;
        }

        var conditions = new List<string>();

        var textColumns = GetSearchableColumns()
            .Where(col => !col.Contains("id", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (textColumns.Count > 0)
        {
            conditions.AddRange(textColumns.Select(col => $"LOWER({col}) LIKE @SearchTerm"));
            parameters.Add("SearchTerm", $"%{searchTerm.ToLower()}%");
        }

        var numericColumns = GetNumericSearchableColumns()
            .Where(col => !col.Contains("id", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (numericColumns.Count > 0 && decimal.TryParse(searchTerm, out var numericValue))
        {
            foreach (var col in numericColumns)
            {
                conditions.Add($"{col} = @NumericSearchTerm");
            }
            parameters.Add("NumericSearchTerm", numericValue);
        }

        var dateColumns = GetDateSearchableColumns();
        if (dateColumns.Count > 0 && DateTime.TryParse(searchTerm, out var dateValue))
        {
            foreach (var col in dateColumns)
            {
                conditions.Add($"DATE({col}) = @DateSearchTerm");
            }
            parameters.Add("DateSearchTerm", dateValue.Date);
        }
        else if (dateColumns.Count > 0)
        {
            foreach (var col in dateColumns)
            {
                conditions.Add($"DATE_FORMAT({col}, '%Y-%m-%d') LIKE @DateTextSearchTerm");
            }
            parameters.Add("DateTextSearchTerm", $"%{searchTerm}%");
        }

        return conditions.Count > 0 ? $"({string.Join(" OR ", conditions)})" : string.Empty;
    }

    private string BuildOrderByClause(string? sortColumn, string? sortOrder)
    {
        var sortableColumns = GetSortableColumns();

        if (string.IsNullOrWhiteSpace(sortColumn) || !sortableColumns.TryGetValue(sortColumn, out var dbColumn))
        {
            return GetDefaultOrderBy();
        }

        var order = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        return $"ORDER BY {dbColumn} {order}";
    }

    #endregion

    #region IReadRepository<T, TReadModel, TId>

    public virtual async Task<TReadModel?> GetReadModelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            var cacheKey = $"{_config.TableName}:{id}";
            var cachedData = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return JsonSerializer.Deserialize<TReadModel>(cachedData);
            }
        }

        using var connection = _dbConnectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("id", id);

        var sql = BuildGetByIdQuery();
        var result = await connection.QueryFirstOrDefaultAsync<TReadModel>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        if (result != null && _cache != null)
        {
            var cacheKey = $"{_config.TableName}:{id}";
            var serialized = JsonSerializer.SerializeToUtf8Bytes(result);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            await _cache.SetAsync(cacheKey, serialized, options, cancellationToken);
        }

        return result;
    }

    public virtual async Task<IEnumerable<TReadModel>> GetAllReadModelsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var sql = BuildGetAllQuery();

        return await connection.QueryAsync<TReadModel>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public virtual async Task<PagedList<TReadModel>> GetPagedReadModelsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();

        var offset = (page - 1) * pageSize;
        var baseQuery = BuildGetPagedQuery();
        var countQuery = BuildCountQuery();
        var orderBy = GetDefaultOrderBy();

        var sql = $"{baseQuery}\n{orderBy}\nLIMIT @PageSize OFFSET @Offset;\n\n{countQuery};";

        var parameters = new DynamicParameters();
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<TReadModel>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedList<TReadModel>(items, page, pageSize, total);
    }

    public virtual async Task<PagedList<TReadModel>> GetPagedReadModelsByUserAsync(
        Guid usuarioId,
        int page,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();

        var offset = (page - 1) * pageSize;
        var baseQuery = BuildGetPagedQuery();
        var countQuery = BuildCountQuery();
        var userIdColumn = GetUserIdColumn();

        var parameters = new DynamicParameters();
        parameters.Add("usuarioId", usuarioId);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var whereClauses = new List<string> { $"{userIdColumn} = @usuarioId" };

        var searchWhereClause = BuildSearchWhereClause(searchTerm ?? string.Empty, parameters);
        if (!string.IsNullOrWhiteSpace(searchWhereClause))
        {
            whereClauses.Add(searchWhereClause);
        }

        var whereClause = $"WHERE {string.Join(" AND ", whereClauses)}";
        var orderBy = BuildOrderByClause(sortColumn, sortOrder);

        var sql = $"{baseQuery}\n{whereClause}\n{orderBy}\nLIMIT @PageSize OFFSET @Offset;\n\n{countQuery}\n{whereClause};";

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<TReadModel>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedList<TReadModel>(items, page, pageSize, total);
    }

    public virtual async Task<IEnumerable<TReadModel>> SearchForAutocompleteAsync(
        Guid usuarioId,
        string searchTerm,
        int limit = 10,
        Dictionary<string, object>? extraFilters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();

        var baseQuery = BuildGetPagedQuery();
        var userIdColumn = GetUserIdColumn();

        var parameters = new DynamicParameters();
        parameters.Add("usuarioId", usuarioId);
        parameters.Add("limit", limit);

        var whereClauses = new List<string> { $"{userIdColumn} = @usuarioId" };

        var searchWhereClause = BuildSearchWhereClause(searchTerm ?? string.Empty, parameters);
        if (!string.IsNullOrWhiteSpace(searchWhereClause))
        {
            whereClauses.Add(searchWhereClause);
        }

        var whereClause = $"WHERE {string.Join(" AND ", whereClauses)}";

        if (extraFilters != null)
        {
            whereClause += BuildDynamicFilters(extraFilters, parameters);
        }

        var orderBy = GetDefaultOrderBy();

        var sql = $"{baseQuery}\n{whereClause}\n{orderBy}\nLIMIT @limit";

        return await connection.QueryAsync<TReadModel>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public virtual async Task<IEnumerable<TReadModel>> GetRecentAsync(
        Guid usuarioId,
        int limit = 5,
        Dictionary<string, object>? extraFilters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();

        var baseQuery = BuildGetPagedQuery();
        var userIdColumn = GetUserIdColumn();
        var tablePrefix = GetTablePrefix();

        var parameters = new DynamicParameters();
        parameters.Add("usuarioId", usuarioId);
        parameters.Add("limit", limit);

        var orderByColumn = $"{tablePrefix}fecha_creacion";

        var sql = $"{baseQuery}\nWHERE {userIdColumn} = @usuarioId";

        if (extraFilters != null)
        {
            sql += BuildDynamicFilters(extraFilters, parameters);
        }

        sql += $"\nORDER BY {orderByColumn} DESC\nLIMIT @limit";

        return await connection.QueryAsync<TReadModel>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Invalida la entrada de caché de un elemento concreto (no forma parte de
    /// <see cref="IReadRepository{T, TDto, TId}"/>, es un extra de conveniencia).
    /// </summary>
    public virtual async Task InvalidateCacheAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            var cacheKey = $"{_config.TableName}:{id}";
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }
    }

    #endregion

    /// <summary>
    /// Añade filtros dinámicos clave/valor al WHERE. El nombre de columna se valida como
    /// identificador seguro (nunca se parametriza en SQL, se interpola tras validar el formato).
    /// </summary>
    protected string BuildDynamicFilters(Dictionary<string, object> filters, DynamicParameters parameters)
    {
        if (filters == null || filters.Count == 0) return string.Empty;

        var conditions = new List<string>();
        foreach (var filter in filters)
        {
            if (!ValidColumnName.IsMatch(filter.Key))
            {
                throw new ArgumentException($"Nombre de columna no válido en filtro dinámico: '{filter.Key}'.", nameof(filters));
            }

            var paramName = $"Filter_{filter.Key.Replace(".", "_")}";
            conditions.Add($"{filter.Key} = @{paramName}");

            var paramValue = filter.Value;
            if (filter.Value is string stringValue && Guid.TryParse(stringValue, out var guidValue))
            {
                paramValue = guidValue;
            }

            parameters.Add(paramName, paramValue);
        }

        return " AND " + string.Join(" AND ", conditions);
    }
}
