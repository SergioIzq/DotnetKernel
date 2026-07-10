using Hangfire;
using SergioIzq.AspNetCore.Kernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace SergioIzq.AspNetCore.Kernel.DependencyInjection;

public static class PipelineExtensions
{
    /// <summary>
    /// Swagger + Swagger UI, solo en Development (en /swagger).
    /// </summary>
    public static WebApplication UseKernelSwaggerUI(this WebApplication app, string apiTitle)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", apiTitle);
            });
        }

        return app;
    }

    /// <summary>
    /// Dashboard de Hangfire en /hangfire, protegido por <see cref="HangfireAuthorizationFilter"/>
    /// (acceso libre solo en Development; en producción responde 403 en vez de exponer el panel).
    /// </summary>
    public static WebApplication UseKernelHangfireDashboard(this WebApplication app, string path = "/hangfire")
    {
        app.UseHangfireDashboard(path, new DashboardOptions
        {
            Authorization = [new HangfireAuthorizationFilter()]
        });

        return app;
    }
}
