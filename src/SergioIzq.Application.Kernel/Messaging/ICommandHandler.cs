using SergioIzq.Domain.Kernel.Abstractions.Results;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging;

public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result> where TCommand : ICommand
{
}

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>> where TCommand : ICommand<TResponse>
{
}
