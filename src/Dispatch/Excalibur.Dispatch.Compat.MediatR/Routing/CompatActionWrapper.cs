using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Canonical <see cref="IDispatchAction{TResponse}"/> envelope that carries a compat
/// <see cref="IRequest{TResponse}"/> through the canonical dispatch middleware pipeline (§9,
/// Option A2). The compat request runs under canonical middleware via
/// <see cref="IDispatchMiddlewareInvoker"/> with a closed, AOT-safe terminal handler — no reflection.
/// </summary>
/// <typeparam name="TRequest">The compat request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class CompatActionWrapper<TRequest, TResponse>(TRequest request) : IDispatchAction<TResponse>
    where TRequest : notnull
{
    /// <summary>Gets the wrapped compat request.</summary>
    public TRequest Request { get; } = request;
}
