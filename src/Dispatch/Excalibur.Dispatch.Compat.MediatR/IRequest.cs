namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Marker interface for a request that returns a response of type <typeparamref name="TResponse"/>.
/// Provides the <c>IRequest&lt;TResponse&gt;</c> shape used by MediatR-based code; maps to the canonical
/// <c>IDispatchAction&lt;TResponse&gt;</c>.
/// </summary>
/// <typeparam name="TResponse">The type of the response produced for this request.</typeparam>
public interface IRequest<out TResponse>;

/// <summary>
/// Marker interface for a request that returns no meaningful response, providing the non-generic
/// <c>IRequest</c> shape used by MediatR-based code. Equivalent to <see cref="IRequest{TResponse}"/>
/// of <see cref="Unit"/>.
/// </summary>
public interface IRequest : IRequest<Unit>;
