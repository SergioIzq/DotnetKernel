using System.Data;
using SergioIzq.Domain.Kernel.Interfaces;
using Dapper;

namespace SergioIzq.Infrastructure.Kernel.TypesHandlers;

/// <summary>
/// Handler de Dapper genérico para cualquier Id fuertemente tipado que implemente
/// <see cref="IGuidValueObject"/>. Cada consumidor registra una instancia por cada tipo de Id
/// concreto que tenga, ej.:
/// <code>SqlMapper.AddTypeHandler(new GuidValueObjectTypeHandler&lt;UsuarioId&gt;(g => UsuarioId.Create(g).Value));</code>
/// </summary>
public class GuidValueObjectTypeHandler<T> : SqlMapper.TypeHandler<T> where T : IGuidValueObject
{
    private readonly Func<Guid, T> _factory;

    public GuidValueObjectTypeHandler(Func<Guid, T> factory)
    {
        _factory = factory;
    }

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = value?.Value ?? Guid.Empty;
        parameter.DbType = DbType.Guid;
    }

    public override T Parse(object value)
    {
        if (value is Guid g) return _factory(g);
        if (value is byte[] bytes && bytes.Length == 16) return _factory(new Guid(bytes));

        throw new DataException($"Cannot convert {value?.GetType()} to {typeof(T).Name}");
    }
}
