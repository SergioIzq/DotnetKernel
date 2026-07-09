# SergioIzq.DotnetKernel

Arquitectura .NET común, extraída de mis proyectos personales (empezando por [Kash](https://github.com/sergioizqdev)) y publicada como paquetes NuGet reutilizables, para no reimplementar lo mismo en cada proyecto nuevo.

## Paquetes

### [`SergioIzq.Domain.Kernel`](src/SergioIzq.Domain.Kernel)

Bloques de dominio genéricos: entidad base con eventos de dominio (`AbsEntity<TId>`), patrón `Result`/`Error`, `PagedList<T>`, y contratos de repositorio/Unit of Work.

```bash
dotnet add package SergioIzq.Domain.Kernel
```

### [`SergioIzq.Logging.HtmlFile`](src/SergioIzq.Logging.HtmlFile)

Sink de Serilog que escribe logs en archivos HTML navegables (con buscador, filtro por nivel y estadísticas en vivo), con filtrado automático de ruido y limpieza periódica de archivos antiguos.

```bash
dotnet add package SergioIzq.Logging.HtmlFile
```

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.WriteToHtmlFile(new HtmlFileLogOptions { PageTitle = "Logs de MiApp" })
    .CreateLogger();

builder.Services.AddHtmlFileLogging();
```

## Roadmap

- `SergioIzq.Application.Kernel` — abstracciones CQRS (Create/Update/Delete/GetPaged/Search) con Template Method.
- `SergioIzq.Infrastructure.Kernel` — middleware de manejo de errores, repositorios base EF Core/Dapper, Unit of Work.
- `@sergioizq/api-core` (npm) — interceptores HTTP, `BaseApiService<T>`, modelos `Result`/`PaginatedList` para Angular.

## Licencia

MIT — ver [LICENSE](LICENSE).
