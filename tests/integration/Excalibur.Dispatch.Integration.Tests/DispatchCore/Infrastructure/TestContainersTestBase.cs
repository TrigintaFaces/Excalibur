// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Hosting;

using Tests.Shared;

using Xunit.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Infrastructure;

/// <summary>
///     Base class for integration tests using TestContainers that provides common test infrastructure.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TestContainersTestBase" /> class. </remarks>
public abstract class TestContainersTestBase(ITestOutputHelper output = null) : IntegrationTestBase
{
	private readonly List<IHost> _hosts = [];
	private readonly ConcurrentBag<IAsyncDisposable> _disposables = [];

	/// <summary>
	///     Gets the test output helper for logging test output.
	/// </summary>
	protected ITestOutputHelper Output { get; } = output;

	/// <summary>
	///     Gets the default timeout for waiting on message delivery.
	/// </summary>
	protected virtual TimeSpan MessageDeliveryTimeout => TimeSpan.FromSeconds(30);

	/// <summary>
	///     Gets the default timeout for container startup.
	/// </summary>
	protected virtual TimeSpan ContainerStartupTimeout => TimeSpan.FromMinutes(2);

	/// <inheritdoc/>
	public override async Task DisposeAsync()
	{
		// Dispose hosts first
		foreach (var host in _hosts)
		{
			await host.StopAsync(TestCancellationToken).ConfigureAwait(false);
			host.Dispose();
		}

		// Dispose other resources
		await Task.WhenAll(_disposables.Select(static d => d.DisposeAsync().AsTask())).ConfigureAwait(false);

		await base.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	///     Creates a message context for testing.
	/// </summary>
	protected static IMessageContext CreateMessageContext(
		string correlationId = null,
		string causationId = null,
		Dictionary<string, object>? headers = null)
	{
		var context = DispatchContextInitializer.CreateDefaultContext();
		context.CorrelationId = correlationId ?? Guid.NewGuid().ToString();
		context.CausationId = causationId;

		if (headers != null)
		{
			foreach (var header in headers)
			{
				context.Items[header.Key] = header.Value;
			}
		}

		return context;
	}

	/// <summary>
	///     Creates a test host with the specified configuration.
	/// </summary>
	protected IHost CreateTestHost(Action<IHostBuilder> configure)
	{
		var hostBuilder = Host.CreateDefaultBuilder()
			.ConfigureLogging(logging =>
			{
				_ = logging.ClearProviders();
				if (Output != null)
				{
					_ = logging.AddXUnit(Output);
				}

				_ = logging.SetMinimumLevel(LogLevel.Debug);
			});

		configure(hostBuilder);

		var host = hostBuilder.Build();
		_hosts.Add(host);
		return host;
	}

	/// <summary>
	///     Creates a test host with dispatch configured.
	/// </summary>
	protected IHost CreateDispatchHost(Action<IDispatchBuilder> configureDispatch, Action<IServiceCollection>? configureServices = null) =>
		CreateTestHost(hostBuilder => hostBuilder.ConfigureServices((context, services) =>
		{
			_ = services.AddDispatch(configureDispatch);
			configureServices?.Invoke(services);
		}));

	/// <summary>
	///     Registers a disposable resource to be cleaned up after the test.
	/// </summary>
	protected void RegisterForDisposal(IAsyncDisposable disposable) => _disposables.Add(disposable);

	/// <summary>
	///     Waits for a message to be received with the specified predicate.
	/// </summary>
	protected async Task<TMessage> WaitForMessageAsync<TMessage>(
		ConcurrentBag<TMessage> receivedMessages,
		Func<TMessage, bool>? predicate = null,
		TimeSpan? timeout = null)
		where TMessage : IDispatchMessage
	{
		var actualTimeout = timeout ?? MessageDeliveryTimeout;
		var endTime = DateTime.UtcNow.Add(actualTimeout);

		while (DateTime.UtcNow < endTime)
		{
			var message = receivedMessages.FirstOrDefault(predicate ?? (static _ => true));
			if (message != null)
			{
				return message;
			}

			await Task.Delay(100, TestCancellationToken).ConfigureAwait(false);
		}

		throw new TimeoutException($"Message of type {typeof(TMessage).Name} was not received within {actualTimeout.TotalSeconds} seconds");
	}

	/// <summary>
	///     Creates a test event handler that records received messages.
	/// </summary>
	protected class RecordingEventHandler<TEvent>(
		ConcurrentBag<TEvent> receivedEvents,
		TaskCompletionSource<TEvent>? firstEventTcs = null,
		Action<TEvent>? onReceived = null) : IEventHandler<TEvent> where TEvent : IDispatchEvent
	{
		private readonly ConcurrentBag<TEvent> _receivedEvents = receivedEvents;
		private readonly TaskCompletionSource<TEvent>? _firstEventTcs = firstEventTcs;
		private readonly Action<TEvent>? _onReceived = onReceived;

		/// <inheritdoc/>
		public Task HandleAsync(TEvent eventMessage, CancellationToken cancellationToken)
		{
			_receivedEvents.Add(eventMessage);
			_onReceived?.Invoke(eventMessage);
			_ = (_firstEventTcs?.TrySetResult(eventMessage));
			return Task.CompletedTask;
		}
	}

	/// <summary>
	///     Creates a test action handler that records received messages.
	/// </summary>
	protected class RecordingActionHandler<TAction, TResult>(
		ConcurrentBag<TAction> receivedActions,
		Func<TAction, TResult> resultFactory) : IActionHandler<TAction, TResult>
		where TAction : IDispatchAction<TResult>
	{
		private readonly ConcurrentBag<TAction> _receivedActions = receivedActions;
		private readonly Func<TAction, TResult> _resultFactory = resultFactory;

		/// <inheritdoc/>
		public Task<TResult> HandleAsync(TAction action, CancellationToken cancellationToken)
		{
			_receivedActions.Add(action);
			var result = _resultFactory(action);
			return Task.FromResult(result);
		}
	}
}
