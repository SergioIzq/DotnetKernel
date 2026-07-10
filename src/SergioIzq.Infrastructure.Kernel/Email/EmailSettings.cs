namespace SergioIzq.Infrastructure.Kernel.Email;

public class EmailSettings
{
    /// <summary>Nombre por defecto de la sección de configuración en appsettings.</summary>
    public const string SectionName = "EmailSettings";

    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;

    public string Username => SmtpUser;
    public string Password => SmtpPass;
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    /// <summary>Dirección Reply-To opcional; si está vacía no se añade la cabecera.</summary>
    public string? ReplyTo { get; set; }
}
