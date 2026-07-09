namespace SergioIzq.Application.Kernel.Interfaces;

/// <summary>
/// Marcador de DI: registra la clase como servicio Scoped en el escaneo automático
/// (ver <see cref="SergioIzq.Application.Kernel.DependencyInjection.MarkerServiceCollectionExtensions"/>).
/// </summary>
public interface IApplicationService
{
}

/// <summary>
/// Marcador de DI: registra la clase como servicio Transient en el escaneo automático.
/// </summary>
public interface ITransientService
{
}

/// <summary>
/// Marcador de DI: registra la clase como servicio Singleton en el escaneo automático.
/// </summary>
public interface ISingletonService
{
}
