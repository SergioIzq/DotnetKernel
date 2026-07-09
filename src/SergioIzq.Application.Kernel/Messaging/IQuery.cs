using SergioIzq.Domain.Kernel.Abstractions.Results;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging;

public interface IQuery<TResult> : IRequest<Result<TResult>>
{
}
