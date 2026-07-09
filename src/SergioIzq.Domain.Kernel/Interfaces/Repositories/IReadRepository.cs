using SergioIzq.Domain.Kernel.Abstractions;
using SergioIzq.Domain.Kernel.Abstractions.Results;

namespace SergioIzq.Domain.Kernel.Interfaces.Repositories;

/// <summary>
/// Interfaz principal para repositorios de lectura optimizados con DTOs.
/// Permite obtener DTOs directamente desde la base de datos sin mapeo intermedio.
/// Esta es la ÚNICA interfaz de lectura que debe usarse en la aplicación.
/// </summary>
public interface IReadRepository<T, TDto, TId>
    where T : AbsEntity<TId>
    where TDto : class
    where TId : IGuidValueObject
{
    /// <summary>
    /// Obtiene un DTO por ID directamente desde la base de datos.
    /// </summary>
    Task<TDto?> GetReadModelByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todos los DTOs directamente desde la base de datos.
    /// </summary>
    Task<IEnumerable<TDto>> GetAllReadModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una página de DTOs directamente desde la base de datos.
    /// Evita el mapeo de Value Objects y mejora el rendimiento.
    /// </summary>
    Task<PagedList<TDto>> GetPagedReadModelsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una página de DTOs filtrada por usuario/propietario con búsqueda y ordenamiento.
    /// Pensada para el patrón habitual de datos personales propiedad de un usuario.
    /// </summary>
    Task<PagedList<TDto>> GetPagedReadModelsByUserAsync(
        Guid usuarioId,
        int page,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Búsqueda rápida para autocomplete (limitada a pocos resultados).
    /// Ideal para selectores asíncronos que necesitan respuestas ultra-rápidas.
    /// </summary>
    /// <param name="usuarioId">ID del usuario propietario</param>
    /// <param name="searchTerm">Término de búsqueda</param>
    /// <param name="limit">Número máximo de resultados (por defecto 10)</param>
    /// <param name="extraFilters">Filtros adicionales opcionales clave/valor</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista limitada de DTOs que coinciden con la búsqueda</returns>
    Task<IEnumerable<TDto>> SearchForAutocompleteAsync(
        Guid usuarioId,
        string searchTerm,
        int limit = 10,
        Dictionary<string, object>? extraFilters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los elementos más recientes de un usuario.
    /// Ideal para pre-cargar selectores con los elementos usados recientemente.
    /// Ordenado por fecha de creación descendente.
    /// </summary>
    /// <param name="usuarioId">ID del usuario propietario</param>
    /// <param name="limit">Número máximo de resultados (por defecto 5)</param>
    /// <param name="extraFilters">Filtros adicionales opcionales clave/valor</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista de los elementos más recientes</returns>
    Task<IEnumerable<TDto>> GetRecentAsync(
        Guid usuarioId,
        int limit = 5,
        Dictionary<string, object>? extraFilters = null,
        CancellationToken cancellationToken = default);
}
