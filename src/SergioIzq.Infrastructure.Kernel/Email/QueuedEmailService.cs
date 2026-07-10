using System.Collections.Concurrent;
using SergioIzq.Application.Kernel.Services;

namespace SergioIzq.Infrastructure.Kernel.Email;

/// <summary>
/// Implementación de <see cref="IEmailService"/> por cola en memoria (ConcurrentQueue):
/// encolar es inmediato y no bloquea la request; <see cref="EmailBackgroundSender"/> drena la cola.
/// </summary>
public class QueuedEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _emailQueue = new();

    public void EnqueueEmail(EmailMessage message)
    {
        _emailQueue.Enqueue(message);
    }

    internal bool TryDequeue(out EmailMessage? message)
    {
        return _emailQueue.TryDequeue(out message);
    }
}
