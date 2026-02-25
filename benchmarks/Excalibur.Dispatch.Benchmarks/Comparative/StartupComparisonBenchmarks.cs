// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention
#pragma warning disable SA1402 // File may only contain a single type - benchmarks with supporting types

/// <summary>
/// Startup time comparison benchmarks.
/// Measures DI container configuration and first-request latency.
/// </summary>
/// <remarks>
/// Sprint 204 - Competitor Comparison Benchmarks Epic.
/// Fills gaps identified in existing MediatRComparisonBenchmarks:
/// - DI container startup time comparison
/// - Startup with multiple handlers
///
/// Performance Targets:
/// - Startup Time: 50% faster than MediatR
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class StartupComparisonBenchmarks
{
	/// <summary>
	/// Baseline: Excalibur container configuration and first resolve.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Dispatch: Container startup")]
	public IDispatcher Dispatch_StartupTime()
	{
		var services = new ServiceCollection();
		_ = services.AddBenchmarkDispatch();
		_ = services.AddTransient<IActionHandler<StartupTestCommand>, StartupTestCommandHandler>();

		using var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IDispatcher>();
	}

	/// <summary>
	/// MediatR: Container configuration and first resolve.
	/// </summary>
	[Benchmark(Description = "MediatR: Container startup")]
	public IMediator MediatR_StartupTime()
	{
		var services = new ServiceCollection();
		_ = services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<StartupComparisonBenchmarks>());

		using var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IMediator>();
	}

	/// <summary>
	/// Dispatch: Container startup with 10 handlers.
	/// </summary>
	[Benchmark(Description = "Dispatch: Startup + 10 handlers")]
	public IDispatcher Dispatch_StartupWith10Handlers()
	{
		var services = new ServiceCollection();
		_ = services.AddBenchmarkDispatch();

		// Register 10 handlers
		_ = services.AddTransient<IActionHandler<StartupTestCommand>, StartupTestCommandHandler>();
		_ = services.AddTransient<IActionHandler<StartupTestCommand2>, StartupTestCommandHandler2>();
		_ = services.AddTransient<IActionHandler<StartupTestCommand3>, StartupTestCommandHandler3>();
		_ = services.AddTransient<IActionHandler<StartupTestCommand4>, StartupTestCommandHandler4>();
		_ = services.AddTransient<IActionHandler<StartupTestCommand5>, StartupTestCommandHandler5>();
		_ = services.AddTransient<IEventHandler<StartupTestEvent>, StartupTestEventHandler1>();
		_ = services.AddTransient<IEventHandler<StartupTestEvent>, StartupTestEventHandler2>();
		_ = services.AddTransient<IEventHandler<StartupTestEvent>, StartupTestEventHandler3>();
		_ = services.AddTransient<IEventHandler<StartupTestEvent2>, StartupTestEventHandler4>();
		_ = services.AddTransient<IEventHandler<StartupTestEvent2>, StartupTestEventHandler5>();

		using var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IDispatcher>();
	}

	/// <summary>
	/// MediatR: Container startup with 10 handlers.
	/// </summary>
	[Benchmark(Description = "MediatR: Startup + 10 handlers")]
	public IMediator MediatR_StartupWith10Handlers()
	{
		var services = new ServiceCollection();
		_ = services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<StartupComparisonBenchmarks>());

		using var sp = services.BuildServiceProvider();
		return sp.GetRequiredService<IMediator>();
	}
}

// ============================================================================
// Startup Test Messages and Handlers (Dispatch)
// ============================================================================

/// <summary>Test command 1 for startup benchmarks.</summary>
public record StartupTestCommand : IDispatchAction
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>Test command 2 for startup benchmarks.</summary>
public record StartupTestCommand2 : IDispatchAction
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>Test command 3 for startup benchmarks.</summary>
public record StartupTestCommand3 : IDispatchAction
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>Test command 4 for startup benchmarks.</summary>
public record StartupTestCommand4 : IDispatchAction
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>Test command 5 for startup benchmarks.</summary>
public record StartupTestCommand5 : IDispatchAction
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>Test event 1 for startup benchmarks.</summary>
public record StartupTestEvent : IDispatchEvent
{
	/// <summary>Gets the data.</summary>
	public string Data { get; init; } = string.Empty;
}

/// <summary>Test event 2 for startup benchmarks.</summary>
public record StartupTestEvent2 : IDispatchEvent
{
	/// <summary>Gets the data.</summary>
	public string Data { get; init; } = string.Empty;
}

/// <summary>Handler 1 for StartupTestCommand.</summary>
public sealed class StartupTestCommandHandler : IActionHandler<StartupTestCommand>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestCommand message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Handler 2 for StartupTestCommand2.</summary>
public sealed class StartupTestCommandHandler2 : IActionHandler<StartupTestCommand2>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestCommand2 message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Handler 3 for StartupTestCommand3.</summary>
public sealed class StartupTestCommandHandler3 : IActionHandler<StartupTestCommand3>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestCommand3 message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Handler 4 for StartupTestCommand4.</summary>
public sealed class StartupTestCommandHandler4 : IActionHandler<StartupTestCommand4>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestCommand4 message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Handler 5 for StartupTestCommand5.</summary>
public sealed class StartupTestCommandHandler5 : IActionHandler<StartupTestCommand5>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestCommand5 message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Event handler 1 for StartupTestEvent.</summary>
public sealed class StartupTestEventHandler1 : IEventHandler<StartupTestEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Event handler 2 for StartupTestEvent.</summary>
public sealed class StartupTestEventHandler2 : IEventHandler<StartupTestEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Event handler 3 for StartupTestEvent.</summary>
public sealed class StartupTestEventHandler3 : IEventHandler<StartupTestEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Event handler 4 for StartupTestEvent2.</summary>
public sealed class StartupTestEventHandler4 : IEventHandler<StartupTestEvent2>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestEvent2 message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Event handler 5 for StartupTestEvent2.</summary>
public sealed class StartupTestEventHandler5 : IEventHandler<StartupTestEvent2>
{
	/// <inheritdoc />
	public Task HandleAsync(StartupTestEvent2 message, CancellationToken cancellationToken) => Task.CompletedTask;
}

// ============================================================================
// Startup Test Messages (MediatR equivalents - registered via assembly scan)
// ============================================================================

/// <summary>MediatR command 1 for startup.</summary>
public record MediatRStartupCommand : IRequest<Unit>
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>MediatR command 2 for startup.</summary>
public record MediatRStartupCommand2 : IRequest<Unit>
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>MediatR command 3 for startup.</summary>
public record MediatRStartupCommand3 : IRequest<Unit>
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>MediatR command 4 for startup.</summary>
public record MediatRStartupCommand4 : IRequest<Unit>
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>MediatR command 5 for startup.</summary>
public record MediatRStartupCommand5 : IRequest<Unit>
{
	/// <summary>Gets the identifier.</summary>
	public int Id { get; init; }
}

/// <summary>MediatR notification 1 for startup.</summary>
public record MediatRStartupEvent : INotification
{
	/// <summary>Gets the data.</summary>
	public string Data { get; init; } = string.Empty;
}

/// <summary>MediatR notification 2 for startup.</summary>
public record MediatRStartupEvent2 : INotification
{
	/// <summary>Gets the data.</summary>
	public string Data { get; init; } = string.Empty;
}

/// <summary>MediatR handler 1.</summary>
public sealed class MediatRStartupHandler1 : IRequestHandler<MediatRStartupCommand, Unit>
{
	/// <inheritdoc />
	public Task<Unit> Handle(MediatRStartupCommand request, CancellationToken cancellationToken) => Task.FromResult(Unit.Value);
}

/// <summary>MediatR handler 2.</summary>
public sealed class MediatRStartupHandler2 : IRequestHandler<MediatRStartupCommand2, Unit>
{
	/// <inheritdoc />
	public Task<Unit> Handle(MediatRStartupCommand2 request, CancellationToken cancellationToken) => Task.FromResult(Unit.Value);
}

/// <summary>MediatR handler 3.</summary>
public sealed class MediatRStartupHandler3 : IRequestHandler<MediatRStartupCommand3, Unit>
{
	/// <inheritdoc />
	public Task<Unit> Handle(MediatRStartupCommand3 request, CancellationToken cancellationToken) => Task.FromResult(Unit.Value);
}

/// <summary>MediatR handler 4.</summary>
public sealed class MediatRStartupHandler4 : IRequestHandler<MediatRStartupCommand4, Unit>
{
	/// <inheritdoc />
	public Task<Unit> Handle(MediatRStartupCommand4 request, CancellationToken cancellationToken) => Task.FromResult(Unit.Value);
}

/// <summary>MediatR handler 5.</summary>
public sealed class MediatRStartupHandler5 : IRequestHandler<MediatRStartupCommand5, Unit>
{
	/// <inheritdoc />
	public Task<Unit> Handle(MediatRStartupCommand5 request, CancellationToken cancellationToken) => Task.FromResult(Unit.Value);
}

/// <summary>MediatR notification handler 1.</summary>
public sealed class MediatRStartupEventHandler1 : INotificationHandler<MediatRStartupEvent>
{
	/// <inheritdoc />
	public Task Handle(MediatRStartupEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>MediatR notification handler 2.</summary>
public sealed class MediatRStartupEventHandler2 : INotificationHandler<MediatRStartupEvent>
{
	/// <inheritdoc />
	public Task Handle(MediatRStartupEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>MediatR notification handler 3.</summary>
public sealed class MediatRStartupEventHandler3 : INotificationHandler<MediatRStartupEvent>
{
	/// <inheritdoc />
	public Task Handle(MediatRStartupEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>MediatR notification handler 4.</summary>
public sealed class MediatRStartupEventHandler4 : INotificationHandler<MediatRStartupEvent2>
{
	/// <inheritdoc />
	public Task Handle(MediatRStartupEvent2 notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>MediatR notification handler 5.</summary>
public sealed class MediatRStartupEventHandler5 : INotificationHandler<MediatRStartupEvent2>
{
	/// <inheritdoc />
	public Task Handle(MediatRStartupEvent2 notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

#pragma warning restore SA1402
#pragma warning restore CA1707
