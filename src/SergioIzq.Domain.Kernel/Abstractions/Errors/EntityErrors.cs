using SergioIzq.Domain.Kernel.Abstractions.Results;

namespace SergioIzq.Domain.Kernel.Abstractions.Errors;

/// <summary>
/// Factorías de errores comunes por entidad. El artículo/género español no puede derivarse
/// del nombre del tipo, así que se pasa la frase completa (ej. "una categoría", "un proveedor").
/// </summary>
public static class EntityErrors
{
    public static Error DuplicateName(string articuloYNombre, string nombre) => Error.Conflict(
        $"Ya existe {articuloYNombre} con el nombre '{nombre}'.");
}
