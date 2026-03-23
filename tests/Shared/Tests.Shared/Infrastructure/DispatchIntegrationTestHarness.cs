// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Shared.Infrastructure;

/// <summary>
/// Reusable integration test harness that wires the full dispatch pipeline with in-memory
/// infrastructure. Provides helpers for dispatching messages and verifying handler execution.
/// </summary>
/// <remarks>
/// <para>
/// Use this harness to write integration tests that exercise the full
/// Command → Dispatch → Pipeline → Handler flow without external dependencies.
/// </para>
/// <para>
/// The harness uses real DI (Microsoft.Extensions.DependencyInjection) and the real
/// dispatch pipeline, with all middleware, but in-memory transports and stores.
/// </para>
/// </remarks>
public sealed class DispatchIntegrationTestHarness : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly HandlerExecutionTracker _tracker;

	/// <summary>
	/// Gets the dispatcher for sending messages through the pipeline.
	/// </summary>
	public IDispatcher Dispatcher { get; }

	/// <summary>
	/// Gets the service provider for resolving additional services.
	/// </summary>
	public IServiceProvider Services => _serviceProvider;

	/// <summary>
	/// Gets the handler execution tracker for verifying handler invocations.
	/// </summary>
	public HandlerExecutionTracker Tracker => _tracker;

	private DispatchIntegrationTestHarness(ServiceProvider serviceProvider, HandlerExecutionTracker tracker)
	{
		_serviceProvider = serviceProvider;
		_tracker = tracker;
		Dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
	}

	/// <summary>
	/// Creates a new harness with the specified configuration.
	/// </summary>
	/// <param name="configure">Optional action to further configure services.</param>
	/// <param name="configureDispatch">Optional action to configure the dispatch builder.</param>
	/// <returns>A configured harness ready for testing.</returns>
	public static DispatchIntegrationTestHarness Create(
		Action<IServiceCollection>? configure = null,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		var tracker = new HandlerExecutionTracker();
		var services = new ServiceCollection();

		services.AddSingleton(tracker);
		services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		services.AddDispatch(dispatch =>
		{
			configureDispatch?.Invoke(dispatch);
		});

		configure?.Invoke(services);

		var provider = services.BuildServiceProvider();
		return new DispatchIntegrationTestHarness(provider, tracker);
	}

	/// <summary>
	/// Dispatches a message and returns the typed result.
	/// </summary>
	/// <typeparam name="TMessage">The message type (must implement IDispatchAction&lt;TResult&gt;).</typeparam>
	/// <typeparam name="TResult">The result type.</typeparam>
	/// <param name="message">The message to dispatch.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The dispatch message result.</returns>
	public async Task<IMessageResult<TResult>> DispatchAsync<TMessage, TResult>(
		TMessage message,
		CancellationToken cancellationToken = default)
		where TMessage : IDispatchAction<TResult>
	{
		var context = DispatchContextInitializer.CreateDefaultContext(_serviceProvider);
		return await Dispatcher.DispatchAsync<TMessage, TResult>(message, context, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Dispatches a fire-and-forget message.
	/// </summary>
	/// <typeparam name="TMessage">The message type (must implement IDispatchMessage).</typeparam>
	/// <param name="message">The message to dispatch.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The dispatch message result.</returns>
	public async Task<IMessageResult> DispatchAsync<TMessage>(
		TMessage message,
		CancellationToken cancellationToken = default)
		where TMessage : IDispatchMessage
	{
		var context = DispatchContextInitializer.CreateDefaultContext(_serviceProvider);
		return await Dispatcher.DispatchAsync(message, context, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await _serviceProvider.DisposeAsync().ConfigureAwait(false);
	}
}

/// <summary>
/// Tracks handler executions during integration tests.
/// Thread-safe for concurrent dispatch scenarios.
/// </summary>
public sealed class HandlerExecutionTracker
{
	private readonly System.Collections.Concurrent.ConcurrentBag<HandlerExecution> _executions = [];

	/// <summary>
	/// Gets all recorded handler executions.
	/// </summary>
	public IReadOnlyList<HandlerExecution> Executions => _executions.ToList();

	/// <summary>
	/// Records a handler execution.
	/// </summary>
	/// <param name="handlerType">The type of the handler.</param>
	/// <param name="messageType">The type of the message.</param>
	/// <param name="message">The message instance.</param>
	/// <param name="result">The result, if any.</param>
	public void Record(Type handlerType, Type messageType, object message, object? result = null)
	{
		_executions.Add(new HandlerExecution(handlerType, messageType, message, result, DateTimeOffset.UtcNow));
	}

	/// <summary>
	/// Gets the count of handler executions for a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to count.</typeparam>
	/// <returns>The number of executions.</returns>
	public int CountFor<TMessage>() =>
		_executions.Count(e => e.MessageType == typeof(TMessage));

	/// <summary>
	/// Gets handler executions for a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to filter by.</typeparam>
	/// <returns>The matching executions.</returns>
	public IReadOnlyList<HandlerExecution> GetFor<TMessage>() =>
		_executions.Where(e => e.MessageType == typeof(TMessage)).ToList();

	/// <summary>
	/// Clears all recorded executions.
	/// </summary>
	public void Clear() => _executions.Clear();
}

/// <summary>
/// Represents a single handler execution.
/// </summary>
public sealed record HandlerExecution(
	Type HandlerType,
	Type MessageType,
	object Message,
	object? Result,
	DateTimeOffset ExecutedAt);
