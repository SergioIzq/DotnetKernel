namespace SergioIzq.Application.Kernel.Services;

public interface IEmailService
{
    void EnqueueEmail(EmailMessage message);
}
