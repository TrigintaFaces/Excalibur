// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Excessive class coupling -- tests for reflection-heavy broker require many DI types

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Tests for <see cref="EventNotificationBroker"/> cached reflection and
/// ExceptionDispatchInfo error handling paths introduced in the projection subsystem
/// quality hardening.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventNotificationBrokerReflectionCacheShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly ServiceCollection _services = new();

	private EventNotificationBroker CreateBroker(IServiceProvider? sp = null)
	{
		if (sp == null)
		{
			_services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());
			sp = _services.BuildServiceProvider();
		}

		var processor = new InlineProjectionProcessor(
			_registry,
			sp.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<InlineProjectionProcessor>.Instance);

		return new EventNotificationBroker(
			processor,
			sp.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(new EventNotificationOptions()),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());
	}

	private static EventNotificationContext CreateContext() =>
		new("order-1", "Order", 1, DateTimeOffset.UtcNow);

	[Fact]
	public async Task CacheReflection_AcrossMultipleNotifications()
	{
		// Arrange — register handler, invoke twice to exercise the cache path
		var invokeCount = 0;
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());
		var handler = new DelegatingHandler<TestOrderPlaced>((_, _, _) =>
		{
			Interlocked.Increment(ref invokeCount);
			return Task.CompletedTask;
		});
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler);
		var sp = services.BuildServiceProvider();
		var broker = CreateBroker(sp);

		var events = new List<IDomainEvent> { new TestOrderPlaced() };

		// Act — invoke twice (second call uses cached reflection)
		await broker.NotifyAsync(events, CreateContext(), CancellationToken.None).ConfigureAwait(false);
		await broker.NotifyAsync(events, CreateContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		invokeCount.ShouldBe(2);
	}

	[Fact]
	public async Task HandleMultipleEventTypes_WithCachedReflection()
	{
		// Arrange — two different event types, each uses its own cache entry
		var placedCount = 0;
		var shippedCount = 0;
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());

		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(
			new DelegatingHandler<TestOrderPlaced>((_, _, _) =>
			{
				Interlocked.Increment(ref placedCount);
				return Task.CompletedTask;
			}));
		services.AddSingleton<IEventNotificationHandler<TestOrderShipped>>(
			new DelegatingHandler<TestOrderShipped>((_, _, _) =>
			{
				Interlocked.Increment(ref shippedCount);
				return Task.CompletedTask;
			}));
		var sp = services.BuildServiceProvider();
		var broker = CreateBroker(sp);

		// Act — mixed event list
		await broker.NotifyAsync(
			new List<IDomainEvent>
			{
				new TestOrderPlaced(),
				new TestOrderShipped(),
				new TestOrderPlaced(),
			},
			CreateContext(),
			CancellationToken.None).ConfigureAwait(false);

		// Assert — each event type resolved correctly via reflection cache
		placedCount.ShouldBe(2);
		shippedCount.ShouldBe(1);
	}

	[Fact]
	public async Task ExceptionDispatchInfo_PreservesOriginalStackTrace_OnPropagate()
	{
		// Arrange — handler throws, Propagate policy should use ExceptionDispatchInfo.Throw
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());

		var thrownException = new InvalidOperationException("original error");
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(
			new DelegatingHandler<TestOrderPlaced>((_, _, _) => throw thrownException));
		var sp = services.BuildServiceProvider();

		var processor = new InlineProjectionProcessor(
			_registry,
			sp.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<InlineProjectionProcessor>.Instance);

		var options = new EventNotificationOptions
		{
			FailurePolicy = NotificationFailurePolicy.Propagate,
		};

		var broker = new EventNotificationBroker(
			processor,
			sp.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(options),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());

		// Act & Assert — ExceptionDispatchInfo.Throw preserves the original exception
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			broker.NotifyAsync(
				new List<IDomainEvent> { new TestOrderPlaced() },
				CreateContext(),
				CancellationToken.None)).ConfigureAwait(false);

		// The original exception (not wrapped in TargetInvocationException)
		ex.Message.ShouldBe("original error");
	}

	[Fact]
	public async Task LogAndContinue_SwallowsTargetInvocationException()
	{
		// Arrange — handler throws, LogAndContinue should not propagate
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());
		services.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(
			new DelegatingHandler<TestOrderPlaced>((_, _, _) =>
				throw new InvalidOperationException("swallowed")));
		var sp = services.BuildServiceProvider();

		var processor = new InlineProjectionProcessor(
			_registry,
			sp.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<InlineProjectionProcessor>.Instance);

		var options = new EventNotificationOptions
		{
			FailurePolicy = NotificationFailurePolicy.LogAndContinue,
		};

		var broker = new EventNotificationBroker(
			processor,
			sp.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(options),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());

		// Act — should NOT throw
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public void InvokeConfigureProjection_DuringConstruction()
	{
		// Arrange — verify IConfigureProjection instances are eagerly invoked
		// Note: IConfigureProjection is internal, so we use a concrete test implementation
		var testConfig = new TestConfigureProjection();

		var sp = _services.BuildServiceProvider();
		var processor = new InlineProjectionProcessor(
			_registry, sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<InlineProjectionProcessor>.Instance);

		// Act
		_ = new EventNotificationBroker(
			processor,
			sp.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(new EventNotificationOptions()),
			NullLogger<EventNotificationBroker>.Instance,
			new EventNotificationServiceCollectionExtensions.IConfigureProjection[] { testConfig });

		// Assert — configuration was eagerly invoked
		testConfig.ConfigureCount.ShouldBe(1);
	}

	private sealed class TestConfigureProjection : EventNotificationServiceCollectionExtensions.IConfigureProjection
	{
		public int ConfigureCount { get; private set; }

		public void Configure() => ConfigureCount++;
	}

	// --- Helpers ---

	private sealed class DelegatingHandler<TEvent> : IEventNotificationHandler<TEvent>
		where TEvent : IDomainEvent
	{
		private readonly Func<TEvent, EventNotificationContext, CancellationToken, Task> _handler;

		public DelegatingHandler(Func<TEvent, EventNotificationContext, CancellationToken, Task> handler)
			=> _handler = handler;

		public Task HandleAsync(TEvent @event, EventNotificationContext context, CancellationToken cancellationToken)
			=> _handler(@event, context, cancellationToken);
	}
}
