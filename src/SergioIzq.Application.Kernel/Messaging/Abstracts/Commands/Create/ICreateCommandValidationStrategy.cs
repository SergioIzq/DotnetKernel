using SergioIzq.Domain.Kernel.Abstractions.Results;

namespace SergioIzq.Application.Kernel.Messaging.Abstracts.Commands;

public interface ICreateCommandValidationStrategy<TCommand>
{
    Task<Result> ValidateAsync(TCommand command, CancellationToken cancellationToken);
}

public class NoValidationStrategy<TCommand> : ICreateCommandValidationStrategy<TCommand>
{
    public Task<Result> ValidateAsync(TCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }
}
