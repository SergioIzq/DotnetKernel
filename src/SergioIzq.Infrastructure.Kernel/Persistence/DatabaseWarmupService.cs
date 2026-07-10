using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

/// <summary>
/// Pre-calienta el pool de conexiones a la base de datos al iniciar la aplicación
/// (5 conexiones con SELECT 1), eliminando el cold start de la primera request.
/// Los errores no son críticos: se loggean como warning y la app arranca igual.
/// </summary>
public class DatabaseWarmupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseWarmupService> _logger;

    public DatabaseWarmupService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Iniciando warm-up de conexiones a base de datos...");

            using var scope = _serviceProvider.CreateScope();
            var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

            var tasks = Enumerable.Range(0, 5).Select(async _ =>
            {
                using var connection = connectionFactory.CreateConnection();
                connection.Open();
                await connection.QuerySingleAsync<int>("SELECT 1", cancellationToken);
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Warm-up completado. Pool de conexiones listo.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error durante warm-up de conexiones (no crítico)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
