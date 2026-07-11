namespace SergioIzq.Infrastructure.Kernel.Scheduling;

/// <summary>
/// Convierte reglas de frecuencia legibles ("diaria"/"daily", "semanal"/"weekly",
/// "mensual"/"monthly", "anual"/"yearly"/"annual") a expresiones CRON, tomando
/// minuto/hora/día/mes de la fecha de ejecución. Pensado para programar jobs
/// recurrentes con <see cref="JobSchedulingService"/>.
/// </summary>
public static class CronExpressionConverter
{
    /// <summary>
    /// Convierte una frecuencia y una fecha de ejecución en una expresión CRON.
    /// </summary>
    /// <exception cref="ArgumentException">Si la frecuencia no es una de las soportadas.</exception>
    public static string ConvertirFrecuenciaACron(string frecuencia, DateTime fechaEjecucion)
    {
        ReadOnlySpan<char> frecuenciaSpan = frecuencia.AsSpan();

        return frecuenciaSpan switch
        {
            _ when frecuenciaSpan.Equals("diaria", StringComparison.OrdinalIgnoreCase)
                || frecuenciaSpan.Equals("daily", StringComparison.OrdinalIgnoreCase)
                => BuildDailyCron(fechaEjecucion),

            _ when frecuenciaSpan.Equals("semanal", StringComparison.OrdinalIgnoreCase)
                || frecuenciaSpan.Equals("weekly", StringComparison.OrdinalIgnoreCase)
                => BuildWeeklyCron(fechaEjecucion),

            _ when frecuenciaSpan.Equals("mensual", StringComparison.OrdinalIgnoreCase)
                || frecuenciaSpan.Equals("monthly", StringComparison.OrdinalIgnoreCase)
                => BuildMonthlyCron(fechaEjecucion),

            _ when frecuenciaSpan.Equals("anual", StringComparison.OrdinalIgnoreCase)
                || frecuenciaSpan.Equals("yearly", StringComparison.OrdinalIgnoreCase)
                || frecuenciaSpan.Equals("annual", StringComparison.OrdinalIgnoreCase)
                => BuildYearlyCron(fechaEjecucion),

            _ => throw new ArgumentException($"Frecuencia no soportada: {frecuencia}")
        };
    }

    private static string BuildDailyCron(DateTime fecha)
        => $"{fecha.Minute} {fecha.Hour} * * *";

    private static string BuildWeeklyCron(DateTime fecha)
        => $"{fecha.Minute} {fecha.Hour} * * {(int)fecha.DayOfWeek}";

    private static string BuildMonthlyCron(DateTime fecha)
        => $"{fecha.Minute} {fecha.Hour} {fecha.Day} * *";

    private static string BuildYearlyCron(DateTime fecha)
        => $"{fecha.Minute} {fecha.Hour} {fecha.Day} {fecha.Month} *";
}
