using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;
using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Interfaces;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

/// <summary>
/// Implementación de <see cref="IDomainValidator"/> con Dapper: comprueba existencia por Id
/// con un <c>SELECT 1 ... LIMIT 1</c>. El nombre de tabla sale del atributo <c>[Table]</c>
/// de la entidad, con fallback a pluralización simple (nombre en minúsculas + "s").
/// </summary>
public class DapperDomainValidator : IDomainValidator
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperDomainValidator(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ExistsAsync<TEntity, TId>(TId id)
        where TEntity : AbsEntity<TId>
        where TId : IGuidValueObject
    {
        using var connection = _connectionFactory.CreateConnection();

        var tableName = GetTableName<TEntity>();
        var realIdValue = id.Value;

        var sql = $"SELECT 1 FROM {tableName} WHERE id = @Id LIMIT 1";

        var result = await connection.ExecuteScalarAsync<int?>(sql, new { Id = realIdValue });

        return result.HasValue;
    }

    private static string GetTableName<TEntity>()
    {
        var type = typeof(TEntity);
        var tableAttr = type.GetCustomAttribute<TableAttribute>();

        if (tableAttr != null && !string.IsNullOrEmpty(tableAttr.Name))
        {
            return tableAttr.Name;
        }

        return type.Name.ToLower() + "s";
    }
}
