## Why

`AbsReadRepository.GetRecentAsync()` es el único método de lectura del kernel que ignora la configuración declarativa de orden de cada repositorio (`ReadRepositoryConfiguration.DefaultOrderBy`): construye su propio `ORDER BY {tabla}.fecha_creacion DESC` a mano, mientras que `GetPagedReadModelsAsync` y `SearchForAutocompleteAsync` sí respetan el `DefaultOrderBy` que cada catálogo declara (normalmente `nombre ASC`). El resultado es que los endpoints "recent" de catálogo (pensados para precargar selectores con los elementos usados recientemente) devuelven la lista ordenada por fecha en vez de alfabéticamente, lo que dificulta escanear visualmente esos elementos en un desplegable.

## What Changes

- `GetRecentAsync` sigue seleccionando los N elementos más recientes por `fecha_creacion DESC` — ese criterio de selección no cambia, es lo que hace útil a "recent".
- El **orden de presentación** del resultado pasa de `fecha_creacion DESC` al orden alfabético que cada repositorio ya declara en `DefaultOrderBy` (p. ej. `nombre ASC`), reutilizando esa configuración existente en vez de introducir una nueva.
- La consulta SQL se reestructura como subconsulta: selección interna por recencia + `LIMIT`, envuelta en un `ORDER BY` externo alfabético — ya que ahora hacen falta dos criterios de orden distintos en la misma consulta.
- Cuando el repositorio usa alias de tabla/joins (p. ej. `Conceptos`, cuyo `DefaultOrderBy` es `c.nombre ASC`), el alias de columna de salida para el `ORDER BY` externo se resuelve automáticamente cruzando `DefaultOrderBy` con `SelectColumns`. Ningún repositorio concreto necesita declarar configuración nueva.
- **BREAKING**: cambia el orden de los elementos que devuelve `IReadRepository<T, TDto, TId>.GetRecentAsync()` (antes: más reciente primero; ahora: alfabético). El conjunto de N elementos seleccionados no cambia, solo el orden en que se devuelven.
- Se actualiza el test de integración `GetRecent_OrdenaPorFechaCreacionDescYRespetaElLimite` (y su nombre) para cubrir ambos criterios a la vez: selección por recencia + orden de salida alfabético, con datos donde ambos criterios difieran.

## Capabilities

### New Capabilities
- `recent-query`: comportamiento de "obtener elementos recientes" de un repositorio de lectura (`IReadRepository.GetRecentAsync` / `AbsReadRepository`) — qué determina qué filas se seleccionan y en qué orden se devuelven al llamador.

### Modified Capabilities
(ninguna — es la primera vez que este comportamiento se documenta como spec)

## Impact

- **Código**: `src/SergioIzq.Infrastructure.Kernel/Persistence/AbsReadRepository.cs` (método `GetRecentAsync`); test de integración en `tests/SergioIzq.Kernel.IntegrationTests/MySql/AbsReadRepositoryMySqlTests.cs`.
- **Paquete**: nueva versión de `SergioIzq.Infrastructure.Kernel` (versionado automático vía Nerdbank.GitVersioning) publicada a NuGet.org al mergear a `main` (`publish-nuget.yml`).
- **Consumidores externos**: cualquier proyecto que use `GetRecentAsync` o `GetRecentQueryHandler<...>` (p. ej. Kash-Backend, con 6 catálogos: Categorías, Cuentas, Proveedores, Personas, Clientes, FormasPago) verá cambiar el orden de sus endpoints "recent" en cuanto actualicen la versión del paquete. No es un cambio silencioso: requiere bump explícito del `PackageReference` en cada proyecto consumidor para adoptarlo.
