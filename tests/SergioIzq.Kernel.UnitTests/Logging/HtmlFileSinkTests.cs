using SergioIzq.Logging.HtmlFile;
using SergioIzq.Logging.HtmlFile.Configuration;
using Serilog;
using Xunit;

namespace SergioIzq.Kernel.UnitTests.Logging;

public class HtmlFileSinkTests
{
    [Fact]
    public void ElSink_EscribeCabeceraHtmlYEntradas()
    {
        // Regresión del bug de la extracción original: shared:true + hooks lanzaba en runtime
        // y la cabecera HTML no podía escribirse nunca.
        var logDir = Path.Combine(Path.GetTempPath(), $"kernel-sink-test-{Guid.NewGuid():N}");

        try
        {
            var logger = new LoggerConfiguration()
                .WriteTo.WriteToHtmlFile(new HtmlFileLogOptions
                {
                    LogDirectory = logDir,
                    FileNamePrefix = "test-",
                    PageTitle = "Tests del Kernel"
                })
                .CreateLogger();

            logger.Error("error de prueba con {Dato}", 42);
            (logger as IDisposable)?.Dispose();

            var file = Assert.Single(Directory.GetFiles(logDir, "*.html"));
            var content = File.ReadAllText(file);

            Assert.StartsWith("<!DOCTYPE html>", content);
            Assert.Contains("Tests del Kernel", content);
            Assert.Contains("log-entry error", content);
            Assert.Contains("error de prueba", content);
        }
        finally
        {
            if (Directory.Exists(logDir)) Directory.Delete(logDir, recursive: true);
        }
    }

    [Fact]
    public void ElFiltro_DescartaInformationSinPalabrasClaveDeBaseDeDatos()
    {
        var logDir = Path.Combine(Path.GetTempPath(), $"kernel-sink-test-{Guid.NewGuid():N}");

        try
        {
            var logger = new LoggerConfiguration()
                .WriteTo.WriteToHtmlFile(new HtmlFileLogOptions
                {
                    LogDirectory = logDir,
                    FileNamePrefix = "test-"
                })
                .CreateLogger();

            logger.Information("mensaje trivial que no debe pasar el filtro");
            logger.Information("ejecutando SaveChanges en la Database");
            (logger as IDisposable)?.Dispose();

            var file = Assert.Single(Directory.GetFiles(logDir, "*.html"));
            var content = File.ReadAllText(file);

            Assert.DoesNotContain("mensaje trivial", content);
            Assert.Contains("SaveChanges", content);
        }
        finally
        {
            if (Directory.Exists(logDir)) Directory.Delete(logDir, recursive: true);
        }
    }
}
