// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventNotificationBrokerShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly TestProjectionStore<OrderSummary> _projectionStore = new();
	private readonly ServiceCollection _services = new();
	private readonly EventNotificationOptions _options = new();

	private EventNotificationBroker CreateBroker(IServiceProvider? sp = null, TimeProvider? timeProvider = null)
	{
		if (sp == null)
		{
			_services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
			sp = _services.BuildServiceProvider();
		}

		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();


		return new EventNotificationBroker(
			scopeFactory,
			Options.Create(_options),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>(), timeProvider);
	}

	private static EventNotificationContext CreateContext() =>
		new("order-1", "Order", 1, DateTimeOffset.UtcNow);

	[Fact]
	public async Task NoOpForEmptyEventsList()
	{
		// Arrange
		var broker = CreateBroker();

		// Act -- should complete without any work
		await broker.NotifyAsync(
			new List<IDomainEvent>(),
			CreateContext());
	}

	[Fact]
	public async Task ThrowOnNullEvents()
	{
		var broker = CreateBroker();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			broker.NotifyAsync(null!, CreateContext()));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var broker = CreateBroker();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			broker.NotifyAsync(new List<IDomainEvent> { new TestOrderPlaced() }, null!));
	}

	[Fact]
	public async Task NotifyHandlersWithoutApplyingAnyProjection()
	{
		// Arrange -- track execution order
		var executionOrder = new List<string>();

		// A registered projection whose apply delegate records if it is ever invoked. It must NOT be:
		// the broker no longer writes projections at all. That is the whole point of the removal.
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
			{
				executionOrder.Add("projection");
				return Task.CompletedTask;
			}));

		// Register notification handler
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
		var handler = new TrackingNotificationHandler(executionOrder);
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler);
		var sp = services.BuildServiceProvider();

		var broker = CreateBroker(sp);

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext());

		// SAFETY -- the broker applied no projection. It used to fold them here, after the events were
		// durable, as a second uncoordinated writer; two concurrent saves could both fold a row and the
		// last write won. The projection host applies in position order instead.
		executionOrder.ShouldNotContain("projection");

		// LIVENESS -- and the notification handler still ran. Without this arm a broker that did nothing
		// at all would satisfy the assertion above, and inaction is the cheapest way to look safe.
		executionOrder.ShouldBe(["handler"]);
	}

	[Fact]
	public async Task InvokeNotificationHandlerForMatchingEventType()
	{
		// Arrange
		var handlerInvoked = false;
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
		var handler = new DelegatingNotificationHandler<TestOrderPlaced>((_, _, _) =>
		{
			handlerInvoked = true;
			return Task.CompletedTask;
		});
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler);
		var sp = services.BuildServiceProvider();

		var broker = CreateBroker(sp);

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext());

		// Assert
		handlerInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task NotCrashWhenHandlerThrowsAndPolicyIsLogAndContinue()
	{
		// Arrange -- handler throws but broker should catch and log with LogAndContinue policy
		_options.FailurePolicy = NotificationFailurePolicy.LogAndContinue;

		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
		var handler = new DelegatingNotificationHandler<TestOrderPlaced>((_, _, _) =>
			throw new InvalidOperationException("handler boom"));
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler);
		var sp = services.BuildServiceProvider();

		var broker = CreateBroker(sp);

		// Act -- should not throw (handler errors are caught and logged)
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext());
	}

	[Fact]
	public async Task PropagateHandlerExceptionWhenPolicyIsPropagate()
	{
		// Arrange -- default policy is Propagate, handler exception should surface
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
		var handler = new DelegatingNotificationHandler<TestOrderPlaced>((_, _, _) =>
			throw new InvalidOperationException("handler boom"));
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler);
		var sp = services.BuildServiceProvider();

		var broker = CreateBroker(sp);

		// Act & Assert -- Propagate policy surfaces the handler exception
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			broker.NotifyAsync(
				new List<IDomainEvent> { new TestOrderPlaced() },
				CreateContext()));

		ex.Message.ShouldBe("handler boom");
	}

	[Fact]
	public async Task InvokeHandlerForEachEventInOrder()
	{
		// Arrange
		var receivedEvents = new List<string>();
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
		var handler = new DelegatingNotificationHandler<TestOrderPlaced>((evt, _, _) =>
		{
			receivedEvents.Add(evt.EventId);
			return Task.CompletedTask;
		});
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler);
		var sp = services.BuildServiceProvider();

		var broker = CreateBroker(sp);

		var event1 = new TestOrderPlaced { EventId = "e1", Version = 1 };
		var event2 = new TestOrderPlaced { EventId = "e2", Version = 2 };

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent> { event1, event2 },
			CreateContext());

		// Assert -- handlers invoked in event order
		receivedEvents.Count.ShouldBe(2);
		receivedEvents[0].ShouldBe("e1");
		receivedEvents[1].ShouldBe("e2");
	}

	[Fact]
	public async Task SucceedWhenNoHandlersRegistered()
	{
		// Arrange -- no handlers, no projections
		var broker = CreateBroker();

		// Act -- should complete without error
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext());
	}

	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var sp = A.Fake<IServiceScopeFactory>();
		var opts = Options.Create(new EventNotificationOptions());
		var logger = NullLogger<EventNotificationBroker>.Instance;

		var configs = Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>();

		// One arm per constructor parameter. The inline projection processor used to be the first of
		// them; it is gone with the dual write, so the guard it had is gone with it rather than being
		// quietly re-pointed at a surviving parameter, which would leave the count looking unchanged.
		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(null!, opts, logger, configs));
		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(sp, null!, logger, configs));
		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(sp, opts, null!, configs));
	}

	[Theory]
	[InlineData(NotificationFailurePolicy.Propagate)]
	[InlineData(NotificationFailurePolicy.LogAndContinue)]
	public async Task BoundTheWaitWithoutDisposingAnUnfinishedHandler(NotificationFailurePolicy policy)
	{
		var clock = new FakeTimeProvider();
		_options.PostCommitTimeout = TimeSpan.FromSeconds(1);
		_options.FailurePolicy = policy;
		var handler = new UncooperativeNotificationHandler();
		var laterCalls = 0;
		var services = new ServiceCollection();
		services.AddScoped<IEventNotificationHandler<TestOrderPlaced>>(_ => handler);
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(
			new DelegatingNotificationHandler<TestOrderPlaced>((_, _, _) =>
			{
				laterCalls++;
				return Task.CompletedTask;
			}));
		await using var provider = services.BuildServiceProvider();
		var broker = CreateBroker(provider, clock);
		var notification = broker.NotifyAsync([new TestOrderPlaced()], CreateContext());
		try
		{
			await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
			clock.Advance(TimeSpan.FromSeconds(1));
			if (policy == NotificationFailurePolicy.Propagate)
			{
				var error = await Should.ThrowAsync<TimeoutException>(() => notification.WaitAsync(TimeSpan.FromSeconds(5)));
				error.Message.ShouldContain("post-commit notification deadline");
			}
			else
			{
				await notification.WaitAsync(TimeSpan.FromSeconds(5));
			}

			handler.Token.IsCancellationRequested.ShouldBeTrue();
			handler.Disposed.Task.IsCompleted.ShouldBeFalse("the callback still owns its scoped dependencies");
			laterCalls.ShouldBe(0);
		}
		finally
		{
			handler.Release.TrySetResult();
		}

		await handler.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
		laterCalls.ShouldBe(0, "expiry must not start another callback after the first finishes");
	}

	// -- Test helpers --

	private sealed class UncooperativeNotificationHandler : IEventNotificationHandler<TestOrderPlaced>, IAsyncDisposable
	{
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public CancellationToken Token { get; private set; }

		public Task HandleAsync(TestOrderPlaced @event, EventNotificationContext context, CancellationToken cancellationToken)
		{
			Token = cancellationToken;
			Started.TrySetResult();
			return Release.Task;
		}

		public ValueTask DisposeAsync()
		{
			Disposed.TrySetResult();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TrackingNotificationHandler : IEventNotificationHandler<TestOrderPlaced>
	{
		private readonly List<string> _order;

		public TrackingNotificationHandler(List<string> order) => _order = order;

		public Task HandleAsync(TestOrderPlaced @event, EventNotificationContext context, CancellationToken cancellationToken)
		{
			_order.Add("handler");
			return Task.CompletedTask;
		}
	}

	private sealed class DelegatingNotificationHandler<TEvent> : IEventNotificationHandler<TEvent>
		where TEvent : IDomainEvent
	{
		private readonly Func<TEvent, EventNotificationContext, CancellationToken, Task> _handler;

		public DelegatingNotificationHandler(
			Func<TEvent, EventNotificationContext, CancellationToken, Task> handler)
			=> _handler = handler;

		public Task HandleAsync(TEvent @event, EventNotificationContext context, CancellationToken cancellationToken)
			=> _handler(@event, context, cancellationToken);
	}
}
