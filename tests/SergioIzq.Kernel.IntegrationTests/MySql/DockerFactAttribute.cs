using System.Diagnostics;
using Xunit;

namespace SergioIzq.Kernel.IntegrationTests.MySql;

/// <summary>
/// Como [Fact], pero el test se marca como omitido si Docker no está disponible en la máquina.
/// En CI (ubuntu-latest) Docker siempre está; en local solo corre con Docker Desktop arrancado.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(DetectDocker);

    public DockerFactAttribute()
    {
        if (!DockerAvailable.Value)
        {
            Skip = "Docker no está disponible en esta máquina (los tests de MySQL corren siempre en CI).";
        }
    }

    private static bool DetectDocker()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format {{.ServerVersion}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null) return false;

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
