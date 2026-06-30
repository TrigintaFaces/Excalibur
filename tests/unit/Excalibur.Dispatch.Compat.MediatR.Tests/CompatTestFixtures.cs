// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

// Shared MediatR-compat test fixtures. These bind ONLY the F2 seam marker types
// (committed @161d64039) so they compile against the skeleton and exercise the
// WS1 adapters once the registration/facade surface lands.
//
// Design note: each scenario uses a DISTINCT request type so an assembly-scan
// registration finds exactly ONE handler per request — avoiding accidental
// duplicate-handler registration (AC-8 fail-fast) bleeding across unrelated tests.

// ---------------------------------------------------------------------------
// Send (AC-1 / AC-2)
// ---------------------------------------------------------------------------

/// <summary>Request used by Send AC-1/AC-2 locks.</summary>
public sealed class Ping : IRequest<string>
{
    public string Name { get; init; } = "Ping";
}

/// <summary>Handler returning the canonical "pong" response (AC-2).</summary>
public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult("pong");
}

/// <summary>Void/Unit request (EC-1).</summary>
public sealed class VoidPing : IRequest;

/// <summary>Void/Unit handler — returns <see cref="Unit.Value"/> (EC-1).</summary>
public sealed class VoidPingHandler : IRequestHandler<VoidPing>
{
    public Task<Unit> Handle(VoidPing request, CancellationToken cancellationToken) => Unit.Task;
}

/// <summary>Request with NO registered handler — AC-7 (handler-not-found).</summary>
public sealed class Orphan : IRequest<string>;

/// <summary>Value-type response request — EC-9 (no boxing on hot path).</summary>
public sealed class NumberQuery : IRequest<int>;

public sealed class NumberQueryHandler : IRequestHandler<NumberQuery, int>
{
    public Task<int> Handle(NumberQuery request, CancellationToken cancellationToken) => Task.FromResult(42);
}

/// <summary>Open-generic request — EC-2 (open-generic handler registration via source-gen).</summary>
public sealed class GenericQuery<T> : IRequest<T>
{
    public T Value { get; init; } = default!;
}

/// <summary>Open-generic handler — echoes the request value (EC-2).</summary>
public sealed class GenericQueryHandler<T> : IRequestHandler<GenericQuery<T>, T>
{
    public Task<T> Handle(GenericQuery<T> request, CancellationToken cancellationToken) => Task.FromResult(request.Value);
}

// ---------------------------------------------------------------------------
// Publish (AC-3 / EC-3)
// ---------------------------------------------------------------------------

/// <summary>Notification fanned out to all handlers — AC-3 / EC-3.</summary>
public sealed class Pinged : INotification;

/// <summary>Notification with GENUINELY no registered handler — EC-3 (zero-handler no-op).</summary>
public sealed class Unheard : INotification;

/// <summary>Records that it was invoked, into a shared sink keyed by handler id.</summary>
public sealed class RecordingNotificationHandler(string id, List<string> sink) : INotificationHandler<Pinged>
{
    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        lock (sink)
        {
            sink.Add(id);
        }

        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Pipeline ordering (AC-5) — distinct request + DI-injected shared order sink.
// Behaviors are OPEN-generic so they register via the landed MediatRCompatOptions
// .AddOpenBehavior(typeof(T<,>)) API (registration order = pipeline order). They are
// pure pass-throughs, so the same shape works for any TResponse.
// ---------------------------------------------------------------------------

/// <summary>Request for the ordered-behavior nesting proof (AC-5).</summary>
public sealed class OrderedPing : IRequest<string>;

/// <summary>Handler that records its position in the shared order sink.</summary>
public sealed class OrderedPingHandler(List<string> order) : IRequestHandler<OrderedPing, string>
{
    public Task<string> Handle(OrderedPing request, CancellationToken cancellationToken)
    {
        order.Add("handler");
        return Task.FromResult("ordered-pong");
    }
}

/// <summary>Base for ordered open-generic behaviors: appends "{Id}-before"/"{Id}-after" around next.</summary>
public abstract class OrderRecordingBehaviorBase<TRequest, TResponse>(string id, List<string> order)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        order.Add($"{id}-before");
        var response = await next(cancellationToken);
        order.Add($"{id}-after");
        return response;
    }
}

public sealed class BehaviorA<TRequest, TResponse>(List<string> order)
    : OrderRecordingBehaviorBase<TRequest, TResponse>("A", order) where TRequest : notnull;

public sealed class BehaviorB<TRequest, TResponse>(List<string> order)
    : OrderRecordingBehaviorBase<TRequest, TResponse>("B", order) where TRequest : notnull;

public sealed class BehaviorC<TRequest, TResponse>(List<string> order)
    : OrderRecordingBehaviorBase<TRequest, TResponse>("C", order) where TRequest : notnull;

// ---------------------------------------------------------------------------
// Short-circuit (EC-4) — distinct request
// ---------------------------------------------------------------------------

/// <summary>Request whose behavior short-circuits before the handler (EC-4).</summary>
public sealed class ShortCircuitPing : IRequest<string>;

/// <summary>Handler that records if reached — must NOT run when short-circuited (EC-4).</summary>
public sealed class ShortCircuitPingHandler(List<string> sink) : IRequestHandler<ShortCircuitPing, string>
{
    public Task<string> Handle(ShortCircuitPing request, CancellationToken cancellationToken)
    {
        sink.Add("handler-ran");
        return Task.FromResult("reached-handler");
    }
}

/// <summary>Open-generic behavior that short-circuits — never calls next (EC-4).</summary>
public sealed class ShortCircuitBehavior<TRequest, TResponse>(List<string> sink) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        sink.Add("short-circuited");
        return Task.FromResult<TResponse>(default!);
    }
}

// ---------------------------------------------------------------------------
// Throw-propagation (EC-5) — distinct request
// ---------------------------------------------------------------------------

/// <summary>Request whose behavior throws (EC-5).</summary>
public sealed class ThrowPing : IRequest<string>;

public sealed class ThrowPingHandler : IRequestHandler<ThrowPing, string>
{
    public Task<string> Handle(ThrowPing request, CancellationToken cancellationToken) => Task.FromResult("never");
}

/// <summary>Marker exception type used to assert throw-propagation through the pipeline (EC-5).</summary>
public sealed class PipelineBoomException(string message) : Exception(message);

/// <summary>Open-generic behavior that throws — proves exception propagates un-swallowed (EC-5).</summary>
public sealed class ThrowingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => throw new PipelineBoomException("behavior boom");
}

// ---------------------------------------------------------------------------
// Stream (AC-6 / C2)
// ---------------------------------------------------------------------------

/// <summary>Stream request yielding a sequence — AC-6 (CreateStream / C2).</summary>
public sealed class Counter : IStreamRequest<int>;

public sealed class CounterHandler : IStreamRequestHandler<Counter, int>
{
    public async IAsyncEnumerable<int> Handle(
        Counter request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= 3; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}
