// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// End-to-end arms for DI-resolved projection handlers driven by the async projection host, read back
/// through the session token the save returned.
/// </summary>
/// <remarks>
/// These handlers used to be invoked inside <c>SaveAsync</c>, which made the projection immediately
/// readable and made the save a second uncoordinated writer. The host is now the only writer; the arms
/// below drive it through <see cref="IProjectionProcessor.ProcessPendingAsync"/> -- the pull API a host
/// with no background service uses -- and then assert both the folded state and that the read observed
/// this particular write.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ProjectionHandlerSessionConsistencyShould
{
	[Fact]
	public async Task ResolveHandlerFromDiThroughFullPipeline()
	{
		// Arrange -- WhenHandledBy handler resolved from the scope the host creates per apply
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddTransient<OrderPlacedHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var saved = harness.Append(new TestOrderPlaced { Amount = 500m }, "order-1", version: 1);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		var projected = read.Projection.ShouldNotBeNull();
		projected.Total.ShouldBe(500m);
		projected.EventCount.ShouldBe(1);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task MixLambdaAndDiHandlersThroughPipeline()
	{
		// Arrange -- When<T> lambda + WhenHandledBy in the same projection
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddSingleton<ILogger<OrderShippedHandler>>(
			NullLogger<OrderShippedHandler>.Instance);
		_ = harness.Services.AddTransient<OrderShippedHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		_ = builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();
		builder.Build();

		var shipped = DateTimeOffset.UtcNow;
		_ = harness.Append(new TestOrderPlaced { Amount = 200m }, "order-1", version: 1);
		var saved = harness.Append(
			new TestOrderShipped { ShippedAt = shipped }, "order-1", version: 2);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert -- both handler shapes applied, in stream order
		processed.EventsApplied.ShouldBe(2);
		var projected = read.Projection.ShouldNotBeNull();
		projected.Total.ShouldBe(200m);
		projected.ShippedAt.ShouldBe(shipped);
		projected.EventCount.ShouldBe(2);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task ApplyOverrideProjectionIdThroughPipeline()
	{
		// Arrange -- the handler routes the fold to a SECOND projection id as well as its own
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddTransient<OrderCancelledWithOverrideHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderCancelled, OrderCancelledWithOverrideHandler>();
		builder.Build();

		var saved = harness.Append(new TestOrderCancelled(), "order-42", version: 3);

		// Act
		var processed = await harness.DriveAsync(safety.Token);

		// Assert -- both the default and the overridden projection exist, and BOTH are fresh for this
		// write. The old arm only checked they were non-null; a fold that reached one id and silently
		// dropped the other would have passed it.
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);

		var byDefault = await harness.ReadAsync<OrderSummary>("order-42", saved, safety.Token);
		_ = byDefault.Projection.ShouldNotBeNull();
		byDefault.Freshness.ShouldBe(ProjectionFreshness.Fresh);

		var overridden = await harness.ReadAsync<OrderSummary>("cancelled-order-42", saved, safety.Token);
		overridden.Projection.ShouldNotBeNull().EventCount.ShouldBe(1);
		overridden.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task HaltWithoutAdvancingWhenAProjectionHandlerThrows()
	{
		// A throwing handler used to be governed by NotificationFailurePolicy, because the fold ran
		// inside the save and the save had to decide whether to fail with it. The host applies the fold
		// now, and its answer is stronger than either policy was: it HALTS at the failed batch and does
		// not advance the checkpoint, so the batch is re-read rather than skipped and the read model can
		// never drift from the event log.
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddTransient<ThrowingHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderFailed, ThrowingHandler>();
		builder.Build();

		var saved = harness.Append(new TestOrderFailed(), "order-1", version: 1);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert -- halted, nothing applied, nothing written, and the read says so plainly rather than
		// reporting a write it never observed.
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted);
		processed.EventsApplied.ShouldBe(0);
		store.Get("order-1").ShouldBeNull("the handler threw before any state could be persisted");
		read.Freshness.ShouldBe(
			ProjectionFreshness.BoundExpired,
			"a halted projection must not report a write as observed");
	}

	[Fact]
	public async Task PassCorrectContextToHandler()
	{
		// Arrange -- the handler's context must carry the stored event's own aggregate metadata
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		var captured = new ContextCapturingHandler.CapturedContext();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddSingleton(captured);
		_ = harness.Services.AddTransient<ContextCapturingHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderRefunded, ContextCapturingHandler>();
		builder.Build();

		var saved = harness.Append(
			new TestOrderRefunded { RefundAmount = 42m },
			"order-99",
			version: 7,
			aggregateType: "SpecialOrder");

		// Act
		var processed = await harness.DriveAsync(safety.Token);

		// Assert -- handler received the aggregate identity the stored event carried
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		captured.AggregateId.ShouldBe("order-99");
		captured.AggregateType.ShouldBe("SpecialOrder");
		captured.CommittedVersion.ShouldBe(7);

		var read = await harness.ReadAsync<OrderSummary>("order-99", saved, safety.Token);
		read.Projection.ShouldNotBeNull().Total.ShouldBe(42m);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task CreateNewProjectionWhenNoneExists()
	{
		// Arrange -- empty store, so the handler must be handed a new TProjection()
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddTransient<OrderPlacedHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var saved = harness.Append(new TestOrderPlaced { Amount = 77m }, "new-order", version: 1);

		// Act
		_ = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("new-order", saved, safety.Token);

		// Assert
		var projected = read.Projection.ShouldNotBeNull();
		projected.Total.ShouldBe(77m);
		projected.EventCount.ShouldBe(1);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task MergeWithExistingProjectionState()
	{
		// Arrange -- pre-seed the store, as a prior batch would have left it
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		await store.UpsertAsync("order-1",
			new OrderSummary { Total = 100m, EventCount = 2 },
			safety.Token);

		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		_ = harness.Services.AddSingleton<ILogger<OrderShippedHandler>>(
			NullLogger<OrderShippedHandler>.Instance);
		_ = harness.Services.AddTransient<OrderShippedHandler>();

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();
		builder.Build();

		var shipped = DateTimeOffset.UtcNow;
		var saved = harness.Append(
			new TestOrderShipped { ShippedAt = shipped }, "order-1", version: 3);

		// Act
		_ = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert -- existing state preserved, new fields updated
		var projected = read.Projection.ShouldNotBeNull();
		projected.Total.ShouldBe(100m); // preserved
		projected.ShippedAt.ShouldBe(shipped); // updated
		projected.EventCount.ShouldBe(3); // incremented
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}
}

/// <summary>
/// Test event used exclusively by ContextCapturingHandler to avoid duplicate detection.
/// </summary>
[MessageName("Test.TestOrderRefunded")]
public sealed class TestOrderRefunded : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 5;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
	public decimal RefundAmount { get; init; } = 50m;
}

/// <summary>
/// Handler that captures context metadata for assertion in tests.
/// Uses TestOrderRefunded to avoid duplicate detection with OrderPlacedHandler.
/// </summary>
internal sealed class ContextCapturingHandler : IProjectionEventHandler<OrderSummary, TestOrderRefunded>
{
	private readonly CapturedContext _captured;

	public ContextCapturingHandler(CapturedContext captured)
	{
		_captured = captured;
	}

	public Task HandleAsync(
		OrderSummary projection,
		TestOrderRefunded @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		_captured.AggregateId = context.AggregateId;
		_captured.AggregateType = context.AggregateType;
		_captured.CommittedVersion = context.CommittedVersion;
		_captured.Timestamp = context.Timestamp;
		projection.Total = @event.RefundAmount;
		projection.EventCount++;
		return Task.CompletedTask;
	}

	internal sealed class CapturedContext
	{
		public string? AggregateId { get; set; }
		public string? AggregateType { get; set; }
		public long CommittedVersion { get; set; }
		public DateTimeOffset Timestamp { get; set; }
	}
}
