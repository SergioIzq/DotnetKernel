using System.Data;
using Dapper;

namespace SergioIzq.Infrastructure.Kernel.TypesHandlers;

/// <summary>
/// Handler de Dapper para <see cref="Guid"/> almacenados como <c>BINARY(16)</c> (convención
/// habitual en MySQL para Ids, más compacta que <c>CHAR(36)</c>).
/// </summary>
public class GuidBinaryTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToByteArray();
        parameter.DbType = DbType.Binary;
    }

    public override Guid Parse(object value)
    {
        return value switch
        {
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            Guid guid => guid,
            string str when !string.IsNullOrEmpty(str) => Guid.Parse(str),
            _ => throw new DataException($"Cannot convert {value?.GetType().Name ?? "null"} to Guid")
        };
    }
}
