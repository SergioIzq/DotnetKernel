## Purpose

Define qué elementos selecciona y en qué orden los devuelve la operación de "recientes" de un repositorio de lectura (`IReadRepository.GetRecentAsync`), usada para precargar selectores de UI con los elementos que el usuario ha creado o usado más recientemente.

## ADDED Requirements

### Requirement: Selección de elementos recientes por fecha de creación
Dado un usuario y un límite N, `GetRecentAsync` SHALL devolver como máximo N elementos pertenecientes a ese usuario, eligiendo entre todos sus elementos (tras aplicar cualquier filtro extra proporcionado) aquellos con `fecha_creacion` más reciente.

#### Scenario: Selecciona los N más recientes ignorando el resto
- **WHEN** un usuario tiene 5 elementos con distintas fechas de creación y se solicitan los 2 más recientes
- **THEN** se devuelven exactamente los 2 elementos con `fecha_creacion` más reciente, excluyendo los otros 3 aunque alguno de ellos preceda alfabéticamente a los seleccionados

#### Scenario: Respeta los filtros extra al seleccionar
- **WHEN** se solicitan elementos recientes con un filtro extra clave/valor (por ejemplo, restringiendo a una categoría concreta)
- **THEN** solo se consideran para la selección por recencia los elementos que además cumplen ese filtro

### Requirement: Orden alfabético del resultado devuelto
El conjunto de elementos seleccionado por `GetRecentAsync` SHALL devolverse ordenado según el orden alfabético por defecto configurado por el repositorio (su `DefaultOrderBy`, típicamente por nombre ascendente), en lugar de por fecha de creación.

#### Scenario: El resultado se reordena alfabéticamente tras la selección
- **WHEN** los elementos recientes seleccionados no están ya en orden alfabético según su fecha de creación (por ejemplo, el más reciente se llama "Zapatería" y el segundo más reciente se llama "Alimentación")
- **THEN** el resultado devuelve "Alimentación" antes que "Zapatería", en el orden alfabético configurado y no en el orden de creación

### Requirement: Orden alfabético correcto en repositorios con alias de tabla
Cuando el repositorio configura sus columnas de selección u orden usando un alias de tabla (por ejemplo, por tener un `JOIN`), `GetRecentAsync` SHALL seguir devolviendo el resultado ordenado alfabéticamente de forma correcta, sin fallar por referenciar una columna o alias que no existe fuera de la consulta interna de selección.

#### Scenario: Repositorio con JOIN y alias de tabla ordena sin error
- **WHEN** el repositorio usa una tabla con alias en un `JOIN` y su orden alfabético por defecto referencia una columna con ese alias (por ejemplo, `c.nombre ASC`)
- **THEN** `GetRecentAsync` devuelve el resultado ordenado alfabéticamente por esa columna sin error de columna o alias desconocido
