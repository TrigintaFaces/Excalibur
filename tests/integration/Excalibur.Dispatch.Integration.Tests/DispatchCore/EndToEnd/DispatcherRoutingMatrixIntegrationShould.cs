// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Bus;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Tests.Shared;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.EndToEnd;

/// <summary>
/// Integration matrix that validates dispatcher routing to local handlers and remote transport buses
/// with and without user-defined middleware.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EndToEnd")]
[Trait("Component", "Messaging")]
public sealed class DispatcherRoutingMatrixIntegrationShould : IntegrationTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Route_Local_Command_To_Local_Handler_With_And_Without_Middleware(bool withMiddleware)
	{
		// Arrange
		await using var fixture = CreateFixture(withMiddleware);
		var command = new LocalRoutedCommand(Guid.NewGuid());
		var context = fixture.ContextFactory.CreateContext();

		// Act
		var result = await fixture.Dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		fixture.LocalCounter.Contains(command.CommandId).ShouldBeTrue();
		fixture.LocalCounter.Count.ShouldBe(1);
		fixture.RemoteBus.DeliveredCount.ShouldBe(0);
		fixture.MiddlewareCounter.Count.ShouldBe(withMiddleware ? 1 : 0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Route_Remote_Command_To_Remote_Transport_With_And_Without_Middleware(bool withMiddleware)
	{
		// Arrange
		await using var fixture = CreateFixture(withMiddleware);
		var command = new RemoteRoutedCommand(Guid.NewGuid());
		var context = fixture.ContextFactory.CreateContext();

		// Act
		var result = await fixture.Dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		fixture.LocalCounter.Count.ShouldBe(0);

		var delivered = await WaitForConditionAsync(
				() => fixture.RemoteBus.Contains(command.CommandId),
				timeout: TimeSpan.FromSeconds(5),
				pollInterval: TimeSpan.FromMilliseconds(10))
			.ConfigureAwait(false);
		delivered.ShouldBeTrue("Remote transport did not receive the dispatched command.");
		fixture.MiddlewareCounter.Count.ShouldBe(withMiddleware ? 1 : 0);
	}

	[Fact]
	public async Task Route_Concurrent_Mixed_Local_And_Remote_Commands_To_Correct_Destinations()
	{
		// Arrange
		await using var fixture = CreateFixture(withMiddleware: true);
		const int messagesPerRoute = 64;
		var tasks = new List<Task<IMessageResult>>(messagesPerRoute * 2);

		// Act
		for (var i = 0; i < messagesPerRoute; i++)
		{
			var local = new LocalRoutedCommand(Guid.NewGuid());
			var remote = new RemoteRoutedCommand(Guid.NewGuid());

			tasks.Add(fixture.Dispatcher.DispatchAsync(
				local,
				fixture.ContextFactory.CreateContext(),
				TestCancellationToken));
			tasks.Add(fixture.Dispatcher.DispatchAsync(
				remote,
				fixture.ContextFactory.CreateContext(),
				TestCancellationToken));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		results.ShouldAllBe(static r => r.Succeeded);
		fixture.LocalCounter.Count.ShouldBe(messagesPerRoute);

		var remoteDelivered = await WaitForConditionAsync(
				() => fixture.RemoteBus.DeliveredCount == messagesPerRoute,
				timeout: TimeSpan.FromSeconds(10),
				pollInterval: TimeSpan.FromMilliseconds(10))
			.ConfigureAwait(false);
		remoteDelivered.ShouldBeTrue("Remote transport did not receive all remotely routed commands.");
		fixture.RemoteBus.DeliveredCount.ShouldBe(messagesPerRoute);
		fixture.MiddlewareCounter.Count.ShouldBe(messagesPerRoute * 2);
	}

	private static RoutingMatrixFixture CreateFixture(bool withMiddleware)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddTransient<LocalRoutedCommandHandler>();
		_ = services.AddTransient<IActionHandler<LocalRoutedCommand>, LocalRoutedCommandHandler>();
		_ = services.AddSingleton<LocalRouteCounter>();
		_ = services.AddSingleton<MiddlewareInvocationCounter>();
		_ = services.AddSingleton<IDispatchRouter, MatrixDispatchRouter>();
		_ = services.AddSingleton<MatrixRemoteTransportBus>();
		_ = services.AddRemoteMessageBus(
			MatrixDispatchRouter.RemoteTransportName,
			static sp => sp.GetRequiredService<MatrixRemoteTransportBus>());

		_ = services.AddDispatch();
		if (withMiddleware)
		{
			_ = services.AddDispatchMiddleware<MatrixProbeMiddleware>();
		}

		return new RoutingMatrixFixture(services.BuildServiceProvider());
	}

	private sealed class RoutingMatrixFixture(ServiceProvider provider) : IAsyncDisposable
	{
		public ServiceProvider Provider { get; } = provider;
		public IDispatcher Dispatcher { get; } = provider.GetRequiredService<IDispatcher>();
		public IMessageContextFactory ContextFactory { get; } = provider.GetRequiredService<IMessageContextFactory>();
		public LocalRouteCounter LocalCounter { get; } = provider.GetRequiredService<LocalRouteCounter>();
		public MatrixRemoteTransportBus RemoteBus { get; } = provider.GetRequiredService<MatrixRemoteTransportBus>();
		public MiddlewareInvocationCounter MiddlewareCounter { get; } = provider.GetRequiredService<MiddlewareInvocationCounter>();

		public async ValueTask DisposeAsync()
		{
			await Provider.DisposeAsync().ConfigureAwait(false);
		}
	}

	private sealed class LocalRouteCounter
	{
		private readonly ConcurrentDictionary<Guid, byte> _seen = new();

		public int Count => _seen.Count;

		public void MarkHandled(Guid commandId) => _seen[commandId] = 1;

		public bool Contains(Guid commandId) => _seen.ContainsKey(commandId);
	}

	private sealed class MiddlewareInvocationCounter
	{
		private int _count;

		public int Count => Volatile.Read(ref _count);

		public void Increment() => _ = Interlocked.Increment(ref _count);
	}

	private sealed class MatrixProbeMiddleware(MiddlewareInvocationCounter counter) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			counter.Increment();
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MatrixDispatchRouter : IDispatchRouter
	{
		internal const string RemoteTransportName = "inmemory-remote";
		private const string LocalTransportName = "local";

		public ValueTask<RoutingDecision> RouteAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			_ = context;
			_ = cancellationToken;

			return message switch
			{
				RemoteRoutedCommand => ValueTask.FromResult(
					RoutingDecision.Success(RemoteTransportName, [RemoteTransportName])),
				LocalRoutedCommand => ValueTask.FromResult(
					RoutingDecision.Success(LocalTransportName, [LocalTransportName])),
				_ => ValueTask.FromResult(
					RoutingDecision.Failure($"No route configured for message type '{message.GetType().Name}'.")),
			};
		}

		public bool CanRouteTo(IDispatchMessage message, string destination)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentException.ThrowIfNullOrWhiteSpace(destination);

			return message switch
			{
				RemoteRoutedCommand => string.Equals(destination, RemoteTransportName, StringComparison.OrdinalIgnoreCase),
				LocalRoutedCommand => string.Equals(destination, LocalTransportName, StringComparison.OrdinalIgnoreCase),
				_ => false,
			};
		}

		public IEnumerable<RouteInfo> GetAvailableRoutes(IDispatchMessage message, IMessageContext context)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentNullException.ThrowIfNull(context);

			return message switch
			{
				RemoteRoutedCommand => [new RouteInfo(RemoteTransportName, RemoteTransportName)],
				LocalRoutedCommand => [new RouteInfo(LocalTransportName, LocalTransportName)],
				_ => [],
			};
		}
	}

	private sealed class MatrixRemoteTransportBus : IMessageBus, IAsyncDisposable
	{
		private readonly InMemoryMessageBusAdapter _adapter;
		private readonly SemaphoreSlim _initGate = new(1, 1);
		private readonly ConcurrentDictionary<Guid, byte> _delivered = new();
		private volatile bool _initialized;

		public MatrixRemoteTransportBus(ILogger<InMemoryMessageBusAdapter> logger)
		{
			_adapter = new InMemoryMessageBusAdapter(logger);
		}

		public int DeliveredCount => _delivered.Count;

		public bool Contains(Guid commandId) => _delivered.ContainsKey(commandId);

		public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
			=> await PublishInternalAsync(action, context, cancellationToken).ConfigureAwait(false);

		public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
			=> await PublishInternalAsync(evt, context, cancellationToken).ConfigureAwait(false);

		public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
			=> await PublishInternalAsync(doc, context, cancellationToken).ConfigureAwait(false);

		public async ValueTask DisposeAsync()
		{
			await _adapter.DisposeAsync().ConfigureAwait(false);
			_initGate.Dispose();
		}

		private async Task PublishInternalAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
			var publishResult = await _adapter.PublishAsync(message, context, cancellationToken).ConfigureAwait(false);
			if (!publishResult.Succeeded)
			{
				throw new InvalidOperationException(publishResult.ErrorMessage ?? "In-memory transport publish failed.");
			}
		}

		private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
		{
			if (_initialized)
			{
				return;
			}

			await _initGate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (_initialized)
				{
					return;
				}

				await _adapter.InitializeAsync(new MatrixRemoteBusOptions { Name = MatrixDispatchRouter.RemoteTransportName }, cancellationToken)
					.ConfigureAwait(false);
				await _adapter.StartAsync(cancellationToken).ConfigureAwait(false);
				await _adapter.SubscribeAsync(
						"matrix-capture",
						CaptureAsync,
						options: null,
						cancellationToken)
					.ConfigureAwait(false);
				_initialized = true;
			}
			finally
			{
				_ = _initGate.Release();
			}
		}

		private Task<IMessageResult> CaptureAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			_ = context;
			_ = cancellationToken;

			if (message is RemoteRoutedCommand command)
			{
				_delivered[command.CommandId] = 1;
			}

			return Task.FromResult<IMessageResult>(MessageResult.Success());
		}
	}

	private sealed class MatrixRemoteBusOptions : IMessageBusOptions;

	private sealed class LocalRoutedCommandHandler(LocalRouteCounter counter) : IActionHandler<LocalRoutedCommand>
	{
		public Task HandleAsync(LocalRoutedCommand action, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			counter.MarkHandled(action.CommandId);
			return Task.CompletedTask;
		}
	}

	private sealed record LocalRoutedCommand(Guid CommandId) : IDispatchAction;

	private sealed record RemoteRoutedCommand(Guid CommandId) : IDispatchAction;
}
