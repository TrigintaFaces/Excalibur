namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Unified entry point combining <see cref="ISender"/> and <see cref="IPublisher"/>, providing the
/// request/response, streaming, and notification operations expected by code written against the
/// <c>IMediator</c> abstraction. Backed by the canonical Excalibur.Dispatch <c>IDispatcher</c>.
/// </summary>
public interface IMediator : ISender, IPublisher;
