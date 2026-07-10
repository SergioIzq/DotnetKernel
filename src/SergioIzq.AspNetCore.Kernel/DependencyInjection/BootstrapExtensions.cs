using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Hangfire;
using Hangfire.MySql;
using SergioIzq.Logging.HtmlFile.Formatters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Serilog;

namespace SergioIzq.AspNetCore.Kernel.DependencyInjection;

/// <summary>
/// Bootstrap de API del kernel: cada método encapsula un bloque de Program.cs que se repetía
/// idéntico entre proyectos. Program.cs queda como una secuencia corta de llamadas de una línea.
/// </summary>
public static class BootstrapExtensions
{
    /// <summary>
    /// Configura Serilog: logger de arranque por consola y logger definitivo a archivo HTML
    /// (formatter + hooks de SergioIzq.Logging.HtmlFile), y engancha UseSerilog al host.
    /// </summary>
    public static WebApplicationBuilder UseKernelSerilog(
        this WebApplicationBuilder builder,
        string appName,
        string logPath = "logs/log.html")
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                formatter: new HtmlLogFormatter(),
                path: logPath,
                rollingInterval: RollingInterval.Day,
                hooks: new HtmlLogHooks($"{appName} Logs")
            )
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    /// <summary>Fija la cultura por defecto de los hilos (números, fechas y textos).</summary>
    public static void SetKernelCulture(string culture)
    {
        var cultureInfo = new CultureInfo(culture);
        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
    }

    /// <summary>
    /// Límites de Kestrel del kernel: 10000 conexiones, body de 10MB, keep-alive de 2 minutos,
    /// HTTP/1.1 + HTTP/2 (HTTP/3 requiere HTTPS obligatorio).
    /// </summary>
    public static IWebHostBuilder ConfigureKernelKestrel(this IWebHostBuilder webHost)
    {
        webHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxConcurrentConnections = 10000;
            options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);

            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
        });

        return webHost;
    }

    /// <summary>
    /// CORS del kernel: política "LocalhostPolicy" (puertos habituales de dev: 4200, 3000,
    /// 5173, 8080) y política "ProductionPolicy" que admite los hosts indicados y sus subdominios.
    /// </summary>
    public static IServiceCollection AddKernelCors(this IServiceCollection services, params string[] productionHosts)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("LocalhostPolicy", policy =>
            {
                policy.WithOrigins(
                        "http://localhost:4200",
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "http://localhost:8080"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("Content-Disposition");
            });

            options.AddPolicy("ProductionPolicy", policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host;

                        return productionHosts.Any(allowed =>
                            host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                            host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
                    }
                    return false;
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// Opciones JSON del kernel para minimal APIs (ConfigureHttpJsonOptions):
    /// camelCase, case-insensitive, ignora nulls al escribir, enums como string.
    /// </summary>
    public static IServiceCollection AddKernelJsonOptions(
        this IServiceCollection services,
        IJsonTypeInfoResolver? appJsonContext = null)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            if (appJsonContext != null)
            {
                options.SerializerOptions.TypeInfoResolverChain.Add(appJsonContext);
            }
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }

    /// <summary>
    /// Controllers + opciones JSON del kernel para MVC (mismas convenciones que
    /// <see cref="AddKernelJsonOptions"/>). Devuelve el IMvcBuilder por si la app
    /// necesita encadenar más configuración.
    /// </summary>
    public static IMvcBuilder AddKernelControllers(
        this IServiceCollection services,
        IJsonTypeInfoResolver? appJsonContext = null)
    {
        return services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                if (appJsonContext != null)
                {
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(appJsonContext);
                }
            });
    }

    /// <summary>
    /// Respuesta uniforme para errores de model binding: { mensaje, errores } con 400.
    /// </summary>
    public static IServiceCollection AddKernelModelValidation(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray());

                return new BadRequestObjectResult(new
                {
                    mensaje = "Error de validación",
                    errores = errors
                });
            };
        });

        return services;
    }

    /// <summary>
    /// Swagger del kernel: security definition Bearer JWT y parámetros en camelCase.
    /// </summary>
    public static IServiceCollection AddKernelSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", document),
                    new List<string>()
                }
            });
            options.DescribeAllParametersInCamelCase();
        });

        return services;
    }

    /// <summary>Compresión Brotli + Gzip, también sobre HTTPS.</summary>
    public static IServiceCollection AddKernelResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        return services;
    }

    /// <summary>
    /// Política de cookies del kernel: HttpOnly siempre, SameSite Lax,
    /// Secure según entorno (SameAsRequest en dev, Always en producción).
    /// </summary>
    public static IServiceCollection AddKernelCookiePolicy(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = context => false;
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.HttpOnly = HttpOnlyPolicy.Always;
            options.Secure = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        return services;
    }

    /// <summary>
    /// Hangfire con storage MySQL (prefijo de tablas "hangfire", poll de 15s) y servidor
    /// con el número de workers indicado.
    /// </summary>
    public static IServiceCollection AddKernelHangfire(
        this IServiceCollection services,
        string connectionString,
        int workerCount = 2)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(connectionString, new MySqlStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(15),
                PrepareSchemaIfNecessary = true,
                TablesPrefix = "hangfire",
            })));

        services.AddHangfireServer(options => options.WorkerCount = workerCount);

        return services;
    }

    /// <summary>
    /// Health checks; si se pasa la cadena de conexión, añade una comprobación real de MySQL
    /// (el endpoint /health deja de ser un simple 200 incondicional).
    /// </summary>
    public static IServiceCollection AddKernelHealthChecks(
        this IServiceCollection services,
        string? mySqlConnectionString = null)
    {
        var healthChecks = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(mySqlConnectionString))
        {
            healthChecks.AddMySql(mySqlConnectionString);
        }

        return services;
    }
}
