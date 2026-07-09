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

### [`SergioIzq.Application.Kernel`](src/SergioIzq.Application.Kernel)

Capa de aplicación CQRS con MediatR: `ICommand`/`IQuery`/handlers, y familias de comandos/queries abstractos con Template Method (Create, Update, Delete, GetById, GetPagedList, Recent, Search) — heredas de ellos e implementas solo los hooks que necesitas. Incluye registro automático de servicios por interfaz marcadora (`IApplicationService`/`ITransientService`/`ISingletonService`) e interfaces cross-cutting (`ICacheService`, `IEmailService`, `IFileStorageService`, `IPasswordHasher`, `IUserContext`, `IJobSchedulingService`, `IDateTimeProvider`).

```bash
dotnet add package SergioIzq.Application.Kernel
```

```csharp
builder.Services.AddMarkedServices(typeof(Program).Assembly);   // registro por marcador
builder.Services.AddApplicationKernelMapping();                  // Mapster: IGuidValueObject -> Guid
```

### [`SergioIzq.Infrastructure.Kernel`](src/SergioIzq.Infrastructure.Kernel)

Repositorios base: `AbsWriteRepository` (EF Core) y `AbsReadRepository` (Dapper, DTOs directos desde SQL con configuración declarativa vía `ReadRepositoryConfiguration`), `UnitOfWork` con rollback automático, `DomainEventDispatcherInterceptor` (único punto de despacho de eventos de dominio — se dispara tras confirmar el guardado, no antes), y registro automático de repositorios con Scrutor. Asume **MySQL** (`LIMIT/OFFSET`, `DATE_FORMAT`) deliberadamente — no es multi-proveedor. Sin dependencia de ASP.NET Core.

```bash
dotnet add package SergioIzq.Infrastructure.Kernel
```

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseMySql(...).AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor>()));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddKernelUnitOfWork();
builder.Services.AddKernelRepositories(typeof(Program).Assembly);
```

> Los repositorios concretos que heredan de `AbsWriteRepository`/`AbsReadRepository` deben ser **públicos** — el escaneo de Scrutor, por defecto, ignora tipos `internal`.

### [`SergioIzq.AspNetCore.Kernel`](src/SergioIzq.AspNetCore.Kernel)

Middleware (`GlobalExceptionHandler` con mapeo de `MySqlException`, `ResultHandlerMiddleware` que corrige automáticamente un `200 OK` cuando el cuerpo es en realidad un `Result` fallido, `NoCacheMiddleware`) y `AbsController`, un controlador base para APIs CQRS con MediatR que traduce `Result`/`Error` a la respuesta HTTP correcta.

```bash
dotnet add package SergioIzq.AspNetCore.Kernel
```

```csharp
app.UseKernelExceptionHandling(); // excepciones -> corrección de Result -> no-cache, en ese orden
```

> Si algún endpoint de tu API usa `[Authorize]`/`[AllowAnonymous]` (como `AbsController`), ASP.NET Core exige tener `app.UseAuthorization()` registrado en el pipeline aunque el endpoint concreto sea anónimo.

## Roadmap

- `@sergioizq/api-core` (npm) — interceptores HTTP, `BaseApiService<T>`, modelos `Result`/`PaginatedList` para Angular.
- Web de seguimiento de versiones de paquetes por proyecto.

## Licencia

MIT — ver [LICENSE](LICENSE).
