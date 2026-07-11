using MediatR;
using SergioIzq.AspNetCore.Kernel.Controllers;
using SergioIzq.AspNetCore.Kernel.Middleware;
using SergioIzq.Domain.Kernel.Abstractions.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace SergioIzq.Kernel.IntegrationTests;

[ApiController]
[Route("test")]
[AllowAnonymous]
public sealed class VerifyController : AbsController
{
    public VerifyController(ISender sender) : base(sender)
    {
    }

    [HttpGet("ok")]
    public IActionResult OkResult() => HandleResult(Result.Success("todo bien"));

    [HttpGet("notfound")]
    public IActionResult NotFoundResult() => HandleResult(Result.Failure<string>(Error.NotFound("no existe")));

    [HttpGet("conflict")]
    public IActionResult ConflictResult() => HandleResult(Result.Failure<string>(Error.Conflict("duplicado")));

    [HttpGet("nocontent")]
    public IActionResult NoContentResult() => HandleResult(Result.Success());

    // A propósito sin pasar por HandleFailure: el ResultHandlerMiddleware debe corregir el 200
    [HttpGet("soft200")]
    public IActionResult Soft200() => Ok(Result.Failure(Error.Validation("dato inválido")));

    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("boom de prueba");
}

public class MiddlewarePipelineTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();

                webBuilder.ConfigureServices(services =>
                {
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MiddlewarePipelineTests).Assembly));
                    services.AddControllers().AddApplicationPart(typeof(VerifyController).Assembly);
                    services.AddAuthorization();
                });

                webBuilder.Configure(app =>
                {
                    app.UseKernelExceptionHandling();
                    app.UseRouting();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });

        _host = await hostBuilder.StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Theory]
    [InlineData("/test/ok", 200)]
    [InlineData("/test/notfound", 404)]
    [InlineData("/test/conflict", 409)]
    [InlineData("/test/nocontent", 204)]
    public async Task AbsController_TraduceElResultAlStatusCorrecto(string path, int expectedStatus)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Fact]
    public async Task ResultHandlerMiddleware_CorrigeUn200ConResultFallido()
    {
        var response = await _client.GetAsync("/test/soft200");

        Assert.Equal(400, (int)response.StatusCode);
    }

    [Fact]
    public async Task GlobalExceptionHandler_ConvierteExcepcionesEn500ConFormaResult()
    {
        var response = await _client.GetAsync("/test/boom");

        Assert.Equal(500, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"isSuccess\":false", body);
        Assert.Contains("System.InternalServerError", body);
    }
}
