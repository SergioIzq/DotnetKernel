using System.Text;
using SergioIzq.AspNetCore.Kernel.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SergioIzq.AspNetCore.Kernel.DependencyInjection;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Configura autenticación JWT Bearer completa a partir de la sección <c>JwtSettings</c>:
    /// validación estricta (issuer/audience/lifetime/firma, ClockSkew cero), lectura del token
    /// desde el header Authorization o la cookie <c>AccessToken</c>, cabecera <c>Token-Expired</c>
    /// cuando el token caducó, y registro de <see cref="JwtSettings"/> y
    /// <see cref="KernelJwtTokenGenerator"/>. Incluye también AddAuthorization().
    /// </summary>
    public static IServiceCollection AddKernelJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = JwtSettings.SectionName)
    {
        var section = configuration.GetSection(sectionName);
        services.Configure<JwtSettings>(section);
        services.AddSingleton<KernelJwtTokenGenerator>();

        var jwtKey = section["SecretKey"] ?? "CLAVE_DEFAULT_INSEGURA_PARA_DEV_CAMBIAME";
        var jwtIssuer = section["Issuer"] ?? string.Empty;
        var jwtAudience = section["Audience"] ?? string.Empty;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero
            };
            options.RequireHttpsMetadata = false; // permitir http en dev

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // 1º header Authorization (estándar); 2º cookie "AccessToken"
                    var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();
                    token ??= context.Request.Cookies["AccessToken"];
                    context.Token = token;

                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }
}
