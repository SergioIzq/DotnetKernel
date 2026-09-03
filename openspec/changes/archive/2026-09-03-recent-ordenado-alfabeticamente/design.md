## Context

`AbsReadRepository<T, TReadModel, TId>.GetRecentAsync()` construye hoy su propio `ORDER BY {tablePrefix}fecha_creacion DESC LIMIT @limit` directamente, sin pasar por `GetDefaultOrderBy()` (que sí usan `GetPagedReadModelsAsync` y `SearchForAutocompleteAsync`, y que lee `_config.DefaultOrderBy` con fallback a `fecha_creacion DESC`). Ver `proposal.md - Why` para la motivación completa.

De los 6 repositorios de catálogo consumidores en Kash-Backend, 5 configuran `defaultOrderBy: "nombre ASC"` sin alias de tabla (`ReadRepositoryConfiguration.Simple`), y 1 (`Conceptos`) usa `ReadRepositoryConfiguration.WithJoins` con alias `c` y `defaultOrderBy: "c.nombre ASC"`.

## Goals / Non-Goals

**Goals:**
- El resultado de `GetRecentAsync` se presenta en el orden alfabético que cada repositorio ya declara en `DefaultOrderBy`, manteniendo la selección de los N elementos por recencia.
- Cero cambios de configuración en los repositorios de catálogo existentes: la resolución del alias de salida para el `ORDER BY` externo es automática.
- Sin cambios en la firma de `IReadRepository<T, TDto, TId>` ni en `GetRecentQueryHandler` (caché de 30s sin tocar).

**Non-Goals:**
- No cambia qué N elementos se seleccionan (sigue siendo por `fecha_creacion DESC`).
- No se añade un parámetro para personalizar el orden por llamada (como `sortColumn`/`sortOrder` en `GetPagedReadModelsByUserAsync`); `GetRecentAsync` siempre usa el `DefaultOrderBy` del repositorio.
- No se actualizan los consumidores externos (p. ej. Kash-Backend) a la nueva versión del paquete; eso es un cambio aparte en su propio repo.

## Decisions

### 1. Envolver la consulta interna en una subconsulta con dos `ORDER BY`
`GetRecentAsync` necesita dos criterios de orden distintos en la misma consulta: uno para decidir qué N filas entran (`fecha_creacion DESC`) y otro para el orden de salida (alfabético). Se resuelve con una tabla derivada:

```sql
SELECT * FROM (
    {baseQuery}
    WHERE {userIdColumn} = @usuarioId [+ filtros extra]
    ORDER BY {tablePrefix}fecha_creacion DESC
    LIMIT @limit
) AS recientes
ORDER BY {columnaAlfabeticaDeSalida}
```

**Alternativa considerada**: ordenar en memoria en C# tras recibir los resultados (`results.OrderBy(...)`). Se descarta porque exigiría asumir que todo `TReadModel` expone una propiedad `Nombre` (no garantizado por el tipo genérico) y rompería la consistencia con el resto de la clase, que ya resuelve el orden en SQL vía configuración declarativa.

### 2. Resolver automáticamente el alias de salida cruzando `DefaultOrderBy` con `SelectColumns`
La columna referenciada en `_config.DefaultOrderBy` (p. ej. `c.nombre` en `"c.nombre ASC"`) solo existe dentro de la subconsulta si usa un alias de `JOIN`; fuera de ella, la tabla derivada solo expone los alias de salida (`Nombre`). `DefaultOrderBy` puede además ser compuesto (varias columnas separadas por coma, p. ej. `"g.fecha DESC, g.id DESC"` en el fixture de test de `Gastos`), así que la resolución debe tratarse término a término, no como una única columna. Para no depender de que cada repositorio declare configuración nueva:
1. Dividir `_config.DefaultOrderBy` por comas en términos independientes (cada uno del tipo `columna [ASC|DESC]`).
2. Para cada término, extraer la referencia de columna y la dirección.
3. Buscar en `_config.SelectColumns` la entrada cuya expresión cruda (antes de ` as `) coincide, en comparación insensible a mayúsculas, con esa referencia.
4. Si se encuentra, sustituir la referencia por su alias de salida (`Nombre`, `Fecha`, `Id`, ...) manteniendo la dirección original.
5. Si un término no encuentra coincidencia (caso defensivo, no debería darse en los repositorios actuales), dejarlo tal cual — funciona igualmente cuando ese término ya es una columna sin alias de tabla.
6. Volver a unir los términos resueltos con comas para formar el `ORDER BY` externo.

**Alternativa considerada**: añadir un campo explícito nuevo a `ReadRepositoryConfiguration` (p. ej. `RecentSortColumn`) que cada repositorio declare junto a `DefaultOrderBy`. Se descarta para evitar tocar los 6 repositorios de catálogo en Kash-Backend por una pieza de configuración derivable automáticamente de datos que ya existen.

### 3. Alcance del cambio limitado a `GetRecentAsync`
No se toca `GetDefaultOrderBy()` ni el resto de métodos de `AbsReadRepository`, que ya respetan `DefaultOrderBy` correctamente. Tampoco se modifica `GetRecentQueryHandler` (capa de aplicación): su clave de caché y TTL de 30s no cambian: lo que cambia es el contenido cacheado (mismo conjunto de elementos, orden distinto).

## Risks / Trade-offs

- **[Riesgo]** Cualquier consumidor del paquete que dependa implícitamente del orden cronológico de `GetRecentAsync` (p. ej. mostrar "el que acabas de crear" arriba del todo) cambiará de comportamiento al adoptar la nueva versión → **Mitigación**: se documenta como **BREAKING** en el proposal; el cambio de orden solo llega a un consumidor cuando este sube explícitamente el `PackageReference`, nunca de forma automática/silenciosa.
- **[Riesgo]** La resolución automática del alias de salida es un parseo ligero de `DefaultOrderBy` por términos separados por coma (heurística de texto, no un parser SQL completo); un término con una expresión compuesta o una función que no aparezca literalmente en `SelectColumns` no encontraría coincidencia → **Mitigación**: ese término concreto se deja tal cual en el `ORDER BY` externo, que ya es el comportamiento correcto cuando no usa alias de tabla (caso de 5 de los 6 catálogos actuales de Kash-Backend).
- **[Riesgo]** Para repositorios genéricos cuyo `DefaultOrderBy` no es alfabético sino cronológico (p. ej. el fixture de test `Gastos`, con `"g.fecha DESC, g.id DESC"`), este cambio no altera el comportamiento observable de `GetRecentAsync` porque el `ORDER BY` externo coincide con el interno → no es un riesgo real, pero conviene que el test de integración lo cubra explícitamente para dejar constancia de que el comportamiento generaliza más allá de "alfabético".
- **[Riesgo]** Envolver la consulta en una subconsulta añade una materialización extra en el motor de BD → **Mitigación**: el `LIMIT` es pequeño (por defecto 5, pensado para precargar selectores), impacto despreciable.

## Migration Plan

- Mergear a `main` dispara `publish-nuget.yml`: build, tests (gate — si falla el test de integración actualizado, no se publica nada), versión automática vía Nerdbank.GitVersioning, y `dotnet nuget push` a NuGet.org.
- No requiere migración de base de datos: el cambio es solo de construcción de la consulta SQL sobre columnas ya existentes.
- Sin plan de rollback especial: si algo falla, revertir el commit en `main` y dejar que la siguiente publicación automática restaure el comportamiento anterior; ningún dato persistido cambia.
- La adopción por parte de consumidores (Kash-Backend u otros) es un paso explícito y separado: cada uno decide cuándo subir la versión del `PackageReference`.
