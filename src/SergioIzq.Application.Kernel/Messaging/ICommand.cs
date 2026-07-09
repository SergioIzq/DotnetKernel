using SergioIzq.Domain.Kernel.Abstractions.Results;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
