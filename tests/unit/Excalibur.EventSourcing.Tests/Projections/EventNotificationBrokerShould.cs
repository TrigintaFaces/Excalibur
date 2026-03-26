// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventNotificationBrokerShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly InMemoryProjectionStore<OrderSummary> _projectionStore = new();
	private readonly ServiceCollection _services = new();
	private readonly EventNotificationOptions _options = new();

	private EventNotificationBroker CreateBroker(IServiceProvider? sp = null)
	{
		if (sp == null)
		{
			_services.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore);
			sp = _services.BuildServiceProvider();
		}

		var processor = new InlineProjectionProcessor(
			_registry,
			sp,
			NullLogger<InlineProjectionProcessor>.Instance);

		return new EventNotificationBroker(
			processor,
			sp,
			Options.Create(_options),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());
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
			CreateContext(),
			CancellationToken.None);
	}

	[Fact]
	public async Task ThrowOnNullEvents()
	{
		var broker = CreateBroker();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			broker.NotifyAsync(null!, CreateContext(), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var broker = CreateBroker();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			broker.NotifyAsync(new List<IDomainEvent> { new TestOrderPlaced() }, null!, CancellationToken.None));
	}

	[Fact]
	public async Task RunInlineProjectionsBeforeHandlers()
	{
		// Arrange -- track execution order
		var executionOrder = new List<string>();

		// Register inline projection
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
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
			CreateContext(),
			CancellationToken.None);

		// Assert -- projections BEFORE handlers (R27.8)
		executionOrder.Count.ShouldBe(2);
		executionOrder[0].ShouldBe("projection");
		executionOrder[1].ShouldBe("handler");
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
			CreateContext(),
			CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task NotCrashWhenHandlerThrows()
	{
		// Arrange -- handler throws but broker should catch and log
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
			CreateContext(),
			CancellationToken.None);
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
			CreateContext(),
			CancellationToken.None);

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
			CreateContext(),
			CancellationToken.None);
	}

	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var processor = new InlineProjectionProcessor(
			_registry, A.Fake<IServiceProvider>(),
			NullLogger<InlineProjectionProcessor>.Instance);
		var sp = A.Fake<IServiceProvider>();
		var opts = Options.Create(new EventNotificationOptions());
		var logger = NullLogger<EventNotificationBroker>.Instance;

		var configs = Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>();

		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(null!, sp, opts, logger, configs));
		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(processor, null!, opts, logger, configs));
		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(processor, sp, null!, logger, configs));
		Should.Throw<ArgumentNullException>(() =>
			new EventNotificationBroker(processor, sp, opts, null!, configs));
	}

	// -- Test helpers --

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
