namespace SergioIzq.AspNetCore.Kernel.Auth;

public class JwtSettings
{
    /// <summary>Nombre por defecto de la sección de configuración en appsettings.</summary>
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 720;
}
