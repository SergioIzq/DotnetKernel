using Dapper;

namespace SergioIzq.Infrastructure.Kernel.TypesHandlers;

public static class DapperTypeHandlerExtensions
{
    /// <summary>
    /// Registra el handler de Guids en formato binario (BINARY(16)). Cada tipo de Id fuertemente
    /// tipado concreto se registra aparte con su propio <see cref="GuidValueObjectTypeHandler{T}"/>
    /// (a diferencia de Kash, este paquete no conoce los tipos de Id de tu dominio).
    /// </summary>
    public static void RegisterGuidBinaryHandler()
    {
        SqlMapper.AddTypeHandler(new GuidBinaryTypeHandler());
    }
}
