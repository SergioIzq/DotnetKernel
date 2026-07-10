using SergioIzq.Application.Kernel.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SergioIzq.Infrastructure.Kernel.Email;

/// <summary>
/// Servicio en segundo plano que drena la cola de <see cref="QueuedEmailService"/> y envía
/// los correos con MailKit. Los fallos de envío se loggean pero no tumban el servicio.
/// </summary>
public class EmailBackgroundSender : BackgroundService
{
    private readonly QueuedEmailService _emailQueue;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailBackgroundSender> _logger;

    public EmailBackgroundSender(
        QueuedEmailService emailQueue,
        IOptions<EmailSettings> options,
        ILogger<EmailBackgroundSender> logger)
    {
        _emailQueue = emailQueue;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de envío de emails en segundo plano iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_emailQueue.TryDequeue(out var message))
            {
                await SendEmailInternalAsync(message!, stoppingToken);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Servicio de envío de emails en segundo plano detenido");
    }

    private async Task SendEmailInternalAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();

        try
        {
            var mimeMessage = new MimeMessage();

            mimeMessage.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));

            if (!string.IsNullOrWhiteSpace(_settings.ReplyTo))
            {
                mimeMessage.ReplyTo.Add(MailboxAddress.Parse(_settings.ReplyTo));
            }

            mimeMessage.To.Add(MailboxAddress.Parse(message.To));
            mimeMessage.Subject = message.Subject;

            var builder = new BodyBuilder { HtmlBody = message.Body };
            mimeMessage.Body = builder.ToMessageBody();

            // SecureSocketOptions.Auto: MailKit decide SSL o StartTLS según el puerto
            // (para el típico 587 usa StartTls automáticamente).
            await client.ConnectAsync(
                _settings.SmtpServer,
                _settings.SmtpPort,
                SecureSocketOptions.Auto,
                cancellationToken);

            if (!string.IsNullOrEmpty(_settings.SmtpUser) && !string.IsNullOrEmpty(_settings.SmtpPass))
            {
                await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);

            _logger.LogInformation("Email enviado a {To}", message.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando a {To}: {Message}", message.To, ex.Message);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
