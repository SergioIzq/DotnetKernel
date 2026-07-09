using SergioIzq.Domain.Kernel.Abstractions.Results;
using MediatR;

namespace SergioIzq.Application.Kernel.Messaging;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>> where TQuery : IQuery<TResponse>
{
}
