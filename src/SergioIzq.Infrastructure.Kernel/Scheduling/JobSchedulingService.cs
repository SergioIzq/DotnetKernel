using System.Linq.Expressions;
using Hangfire;
using SergioIzq.Application.Kernel.Services;

namespace SergioIzq.Infrastructure.Kernel.Scheduling;

/// <summary>
/// Implementación de <see cref="IJobSchedulingService"/> sobre Hangfire.
/// Gestiona la creación, actualización y eliminación de jobs recurrentes.
/// </summary>
public sealed class JobSchedulingService : IJobSchedulingService
{
    public string GenerateJobId()
    {
        return Guid.NewGuid().ToString();
    }

    public Task ScheduleRecurringJobAsync(
        string jobId,
        DateTime fechaInicio,
        string frecuencia,
        Expression<Func<Task>> methodCall)
    {
        RecurringJob.AddOrUpdate(
            recurringJobId: jobId,
            methodCall: methodCall,
            cronExpression: frecuencia,
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Local
            });

        return Task.CompletedTask;
    }

    public Task RemoveRecurringJobAsync(string jobId)
    {
        RecurringJob.RemoveIfExists(jobId);
        return Task.CompletedTask;
    }

    public async Task UpdateRecurringJobAsync(
        string jobId,
        DateTime fechaInicio,
        string frecuencia,
        Expression<Func<Task>> methodCall)
    {
        await RemoveRecurringJobAsync(jobId);
        await ScheduleRecurringJobAsync(jobId, fechaInicio, frecuencia, methodCall);
    }

    /// <summary>
    /// Hangfire no expone una API directa de existencia: se intenta triggear el job
    /// y se interpreta la excepción como "no existe".
    /// </summary>
    public bool JobExists(string jobId)
    {
        try
        {
            RecurringJob.TriggerJob(jobId);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
