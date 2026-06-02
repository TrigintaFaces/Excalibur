// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionBuilderShould
{
	private readonly InMemoryProjectionRegistry _registry = new();

	[Fact]
	public void DefaultToAsyncMode()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- no Inline() or Async() call
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Async);
		registration.InlineApply.ShouldNotBeNull(); // async mode now has apply delegate for AsyncProjectionProcessingHost
	}

	[Fact]
	public void SetInlineMode()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Inline);
		registration.InlineApply.ShouldNotBeNull(); // inline delegate captured
	}

	[Fact]
	public void SetAsyncModeExplicitly()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Async();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Async);
	}

	[Fact]
	public void SupportMultipleEventHandlers()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.When<TestOrderShipped>((proj, e) => proj.ShippedAt = e.ShippedAt);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderShipped));
		projection.HandledEventTypes.Count.ShouldBe(2);
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- all methods return the builder for fluent chaining
		var result = builder
			.Inline()
			.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount)
			.WithCacheTtl(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void SetCacheTtl()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		var ttl = TimeSpan.FromMinutes(10);

		// Act
		builder.WithCacheTtl(ttl);

		// Assert
		builder.CacheTtl.ShouldBe(ttl);
	}

	[Fact]
	public void ReplaceExistingRegistrationOnSecondBuild()
	{
		// Arrange -- first registration as Inline
		var builder1 = new ProjectionBuilder<OrderSummary>(_registry);
		builder1.Inline();
		builder1.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder1.Build();

		// Act -- second registration as Async (R27.37: idempotent, replaces)
		var builder2 = new ProjectionBuilder<OrderSummary>(_registry);
		builder2.Async();
		builder2.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount * 2);
		builder2.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Async);
	}

	[Fact]
	public void SetEphemeralMode()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Ephemeral();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Ephemeral);
		registration.InlineApply.ShouldBeNull(); // ephemeral projections don't have inline delegates
	}

	[Fact]
	public void SupportFluentChainingWithEphemeral()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- Ephemeral() returns builder for fluent chaining
		var result = builder
			.Ephemeral()
			.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount)
			.WithCacheTtl(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void EphemeralOverridesInlineMode()
	{
		// Arrange -- set Inline first, then Ephemeral (last-wins)
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.Ephemeral();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert -- last mode set wins
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Ephemeral);
		registration.InlineApply.ShouldBeNull();
	}

	[Fact]
	public void ThrowOnNullRegistry()
	{
		Should.Throw<ArgumentNullException>(() => new ProjectionBuilder<OrderSummary>((IProjectionRegistry)null!));
	}

	[Fact]
	public void ThrowOnNullHandler()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		Should.Throw<ArgumentNullException>(() =>
			builder.When<TestOrderPlaced>((Action<OrderSummary, TestOrderPlaced>)null!));
	}

	[Fact]
	public async Task InlineApplyDelegateAppliesEventsAndPersists()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.When<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
			proj.EventCount++;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<Dispatch.IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 150m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(150m);
		projected.ShippedAt.ShouldNotBeNull();
		projected.EventCount.ShouldBe(2);
	}

	// --- WhenDeleted tests (R27.23) ---

	[Fact]
	public void SetDeleteAction()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		// Act
		builder.WhenDeleted((id, ct) => Task.CompletedTask);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.DeleteAction.ShouldNotBeNull();
		builder.DeleteAction.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeDeleteActionWithCorrectArguments()
	{
		// Arrange
		string? capturedId = null;
		CancellationToken capturedCt = default;
		var cts = new CancellationTokenSource();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.WhenDeleted((id, ct) =>
		{
			capturedId = id;
			capturedCt = ct;
			return Task.CompletedTask;
		});
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Act
		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		await registration.DeleteAction!("projection-42", cts.Token);

		// Assert
		capturedId.ShouldBe("projection-42");
		capturedCt.ShouldBe(cts.Token);
	}

	[Fact]
	public void ThrowOnNullDeleteAction()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		Should.Throw<ArgumentNullException>(() =>
			builder.WhenDeleted(null!));
	}

	[Fact]
	public void LastDeleteActionWins()
	{
		// Arrange
		var callCount = 0;
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- register two delete actions, last one should win
		builder.WhenDeleted((id, ct) =>
		{
			callCount = 1;
			return Task.CompletedTask;
		});
		builder.WhenDeleted((id, ct) =>
		{
			callCount = 2;
			return Task.CompletedTask;
		});

		// Assert
		builder.DeleteAction!("test", CancellationToken.None);
		callCount.ShouldBe(2);
	}

	[Fact]
	public void ReturnBuilderForFluentChainingWithWhenDeleted()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		var result = builder.WhenDeleted((id, ct) => Task.CompletedTask);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// --- KeyedBy identity tests (formerly IdentityFrom — removed as duplicate of KeyedBy) ---

	[Fact]
	public void SetKeyedByIdentityResolver()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.KeyedBy<TestOrderPlaced>(e => $"custom-{e.AggregateId}");
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert -- KeyedBy sets the key selector for projection identity resolution
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.InlineApply.ShouldNotBeNull();
	}

	[Fact]
	public async Task KeyedByResolvesProjectionIdFromEvent()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.KeyedBy<TestOrderPlaced>(e => $"order-{e.Amount}");
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<Dispatch.IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "agg-1", Amount = 42m, Version = 1 }
		};
		var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- stored under key-resolved ID, not aggregate ID
		store.Get("order-42").ShouldNotBeNull();
		store.Get("order-42")!.Total.ShouldBe(42m);
		store.Get("agg-1").ShouldBeNull(); // not stored under aggregate ID
	}

	[Fact]
	public void ThrowOnNullKeyedBySelector()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		Should.Throw<ArgumentNullException>(() =>
			builder.KeyedBy<TestOrderPlaced>(null!));
	}

	[Fact]
	public void ReturnBuilderForFluentChainingWithKeyedBy()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		var result = builder.KeyedBy<TestOrderPlaced>(e => e.AggregateId);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// --- WithStore tests ---

	[Fact]
	public void SetStoreType()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.WithStore<InMemoryProjectionStore<OrderSummary>>();
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.StoreType.ShouldBe(typeof(InMemoryProjectionStore<OrderSummary>));
		builder.StoreType.ShouldBe(typeof(InMemoryProjectionStore<OrderSummary>));
	}

	[Fact]
	public void ReturnBuilderForFluentChainingWithWithStore()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		var result = builder.WithStore<InMemoryProjectionStore<OrderSummary>>();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DefaultStoreTypeToNull()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.StoreType.ShouldBeNull();
	}

	// --- WithOptions tests ---

	[Fact]
	public void SetProjectionOptions()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		var threshold = TimeSpan.FromMilliseconds(200);

		// Act
		builder.WithOptions(o => o.WarningThreshold = threshold);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Options.ShouldNotBeNull();
		registration.Options!.WarningThreshold.ShouldBe(threshold);
		builder.Options.ShouldNotBeNull();
		builder.Options!.WarningThreshold.ShouldBe(threshold);
	}

	[Fact]
	public void DefaultOptionsWarningThresholdTo100Ms()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- configure options without changing threshold
		builder.WithOptions(_ => { });

		// Assert
		builder.Options.ShouldNotBeNull();
		builder.Options!.WarningThreshold.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void ThrowOnNullOptionsAction()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		Should.Throw<ArgumentNullException>(() =>
			builder.WithOptions(null!));
	}

	[Fact]
	public void ReturnBuilderForFluentChainingWithWithOptions()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		var result = builder.WithOptions(o => o.WarningThreshold = TimeSpan.FromSeconds(1));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void OptionsNullWhenNotConfigured()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Options.ShouldBeNull();
	}

	// --- Full fluent chaining with all 4 new methods ---

	[Fact]
	public void SupportFullFluentChainingWithAllNewMethods()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- all 4 new methods + existing ones in a single fluent chain
		var result = builder
			.Inline()
			.KeyedBy<TestOrderPlaced>(e => e.AggregateId)
			.WithStore<InMemoryProjectionStore<OrderSummary>>()
			.WithOptions(o => o.WarningThreshold = TimeSpan.FromMilliseconds(50))
			.WhenDeleted((id, ct) => Task.CompletedTask)
			.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount)
			.WithCacheTtl(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public async Task SyncOnlyFastPathShouldNotCreateGhostProjectionsForUnrelatedEvents()
	{
		// Arrange -- projection handles only TestOrderPlaced, NOT TestOrderShipped
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;

		// Act -- send only events the projection does NOT handle
		var unrelatedEvents = new List<Dispatch.IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-ghost", Version = 1 },
			new TestOrderCancelled { AggregateId = "order-ghost", Version = 2 },
		};
		var context = new EventNotificationContext("order-ghost", "Order", 2, DateTimeOffset.UtcNow);

		await registration.InlineApply!(unrelatedEvents, context, services, CancellationToken.None);

		// Assert -- no ghost projection created in the store
		store.Get("order-ghost").ShouldBeNull();
	}

	[Fact]
	public async Task SyncOnlyFastPathShouldOnlyProjectMatchingEvents()
	{
		// Arrange -- projection handles TestOrderPlaced but NOT TestOrderShipped
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;

		// Act -- mix of handled and unhandled events
		var events = new List<Dispatch.IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-mix", Version = 1 }, // unhandled
			new TestOrderPlaced { AggregateId = "order-mix", Amount = 77m, Version = 2 }, // handled
			new TestOrderCancelled { AggregateId = "order-mix", Version = 3 }, // unhandled
		};
		var context = new EventNotificationContext("order-mix", "Order", 3, DateTimeOffset.UtcNow);

		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- projection created with only the matched event applied
		var projected = store.Get("order-mix");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(77m);
		projected.EventCount.ShouldBe(0); // When<TestOrderPlaced> only sets Total, not EventCount
	}

	[Fact]
	public async Task InlineApplyDelegateMergesWithExistingState()
	{
		// Arrange -- pre-seed store with existing projection state
		var store = new InMemoryProjectionStore<OrderSummary>();
		await store.UpsertAsync("order-1",
			new OrderSummary { Total = 50m, EventCount = 1 },
			CancellationToken.None);

		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
			proj.EventCount++;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<Dispatch.IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- existing Total preserved, new fields updated
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(50m); // preserved from pre-seed
		projected.ShippedAt.ShouldNotBeNull();
		projected.EventCount.ShouldBe(2); // incremented
	}

	// --- Context-aware When<TEvent>(Action<T, TEvent, ProjectionContext>) tests (bd-vh3mov) ---

	[Fact]
	public void RegisterContextAwareHandler()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e, ctx) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HasContextHandlers.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullContextAwareHandler()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.When<TestOrderPlaced>((Action<OrderSummary, TestOrderPlaced, ProjectionContext>)null!));
	}

	[Fact]
	public void ReturnBuilderForFluentChainingWithContextAwareHandler()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		var result = builder.When<TestOrderPlaced>((proj, e, ctx) => proj.Total = e.Amount);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ContextAwareHandlerAppliesViaMultiStreamProjectionApply()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e, ctx) =>
		{
			proj.Total = ctx.IsReplay ? 0m : e.Amount;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		var state = new OrderSummary();
		var @event = new TestOrderPlaced { Amount = 250m };

		// Act -- Apply with Live context (IsReplay=false)
		var applied = projection.Apply(state, @event, ProjectionContext.Live);

		// Assert
		applied.ShouldBeTrue();
		state.Total.ShouldBe(250m);
	}

	[Fact]
	public void ContextAwareHandlerReceivesReplayContextCorrectly()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e, ctx) =>
		{
			proj.Total = ctx.IsReplay ? -1m : e.Amount;
			proj.EventCount = (int)(ctx.GlobalPosition ?? 0);
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		var state = new OrderSummary();
		var @event = new TestOrderPlaced { Amount = 500m };

		// Act -- Apply with Replay context
		var applied = projection.Apply(state, @event, ProjectionContext.Replay(42L));

		// Assert
		applied.ShouldBeTrue();
		state.Total.ShouldBe(-1m); // IsReplay=true path
		state.EventCount.ShouldBe(42); // GlobalPosition captured
	}

	[Fact]
	public void ApplyWithoutContextDefaultsToLiveContext()
	{
		// Arrange -- use context-aware handler but call Apply without explicit context
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		ProjectionContext? capturedContext = null;
		builder.When<TestOrderPlaced>((proj, e, ctx) =>
		{
			capturedContext = ctx;
			proj.Total = e.Amount;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		var state = new OrderSummary();
		var @event = new TestOrderPlaced { Amount = 100m };

		// Act -- Apply without context uses the overload that defaults to Live
		projection.Apply(state, @event);

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext!.IsReplay.ShouldBeFalse();
		capturedContext.GlobalPosition.ShouldBeNull();
		state.Total.ShouldBe(100m);
	}

	[Fact]
	public void ApplyReturnsFalseForUnhandledEventTypes()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e, ctx) => proj.Total = e.Amount);
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		var state = new OrderSummary();
		var unhandledEvent = new TestOrderCancelled();

		// Act
		var applied = projection.Apply(state, unhandledEvent, ProjectionContext.Live);

		// Assert
		applied.ShouldBeFalse();
		state.Total.ShouldBe(0m);
	}

	[Fact]
	public async Task InlineApplyWithContextHandlerPassesLiveContext()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		ProjectionContext? capturedContext = null;
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e, ctx) =>
		{
			capturedContext = ctx;
			proj.Total = e.Amount;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<Dispatch.IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-ctx", Amount = 300m, Version = 1 }
		};
		var context = new EventNotificationContext("order-ctx", "Order", 1, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- inline apply passes Live context for sync context handlers
		capturedContext.ShouldNotBeNull();
		capturedContext!.IsReplay.ShouldBeFalse();
		store.Get("order-ctx")!.Total.ShouldBe(300m);
	}

	[Fact]
	public void MixSyncAndContextHandlersOnDifferentEventTypes()
	{
		// Arrange -- sync handler for one event type, context handler for another
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount); // sync
		builder.When<TestOrderShipped>((proj, e, ctx) =>                  // context-aware
		{
			proj.ShippedAt = ctx.IsReplay ? null : e.ShippedAt;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;

		// Act & Assert -- both handlers registered
		projection.HandledEventTypes.Count.ShouldBe(2);
		projection.HasContextHandlers.ShouldBeTrue();

		// Apply sync handler
		var state = new OrderSummary();
		projection.Apply(state, new TestOrderPlaced { Amount = 50m });
		state.Total.ShouldBe(50m);

		// Apply context-aware handler with Replay
		projection.Apply(state, new TestOrderShipped(), ProjectionContext.Replay(10L));
		state.ShippedAt.ShouldBeNull(); // IsReplay=true, so set to null
	}
}
