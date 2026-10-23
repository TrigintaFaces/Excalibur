// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// End-to-end arms for immutable (record) projections driven by the async projection host and read back
/// through the session token the save returned.
/// </summary>
/// <remarks>
/// The fold used to run inside <c>SaveAsync</c>, which made the record readable the instant the save
/// returned and made the save a second uncoordinated writer. The host is the only writer now; these arms
/// drive it through <see cref="IProjectionProcessor.ProcessPendingAsync"/> and then assert both the
/// transformed record and that the read observed this particular write.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ImmutableProjectionSessionConsistencyShould
{
	[Fact]
	public async Task CreateAndTransformImmutableProjectionThroughPipeline()
	{
		// Arrange
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderRecord>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderRecord>>(store);

		var builder = new ImmutableProjectionBuilder<OrderRecord>(harness.Services);
		_ = builder.Async();
		_ = builder.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null));
		_ = builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(harness.Registry);

		var shipped = DateTimeOffset.UtcNow;
		_ = harness.Append(new TestOrderPlaced { Amount = 300m }, "order-1", version: 1);
		var saved = harness.Append(
			new TestOrderShipped { ShippedAt = shipped }, "order-1", version: 2);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderRecord>("order-1", saved, safety.Token);

		// Assert -- created by the first event, transformed by the second, both in one batch
		processed.EventsApplied.ShouldBe(2);
		var result = read.Projection.ShouldNotBeNull();
		result.Total.ShouldBe(300m);
		result.ShippedAt.ShouldBe(shipped);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task HaltWithoutAdvancingWhenTransformingWithNoStateToTransform()
	{
		// Arrange -- only WhenTransforming, no WhenCreating, so the first event has nothing to fold into
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderRecord>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderRecord>>(store);

		var builder = new ImmutableProjectionBuilder<OrderRecord>(harness.Services);
		_ = builder.Async();
		_ = builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(harness.Registry);

		var saved = harness.Append(new TestOrderShipped(), "order-1", version: 1);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderRecord>("order-1", saved, safety.Token);

		// Assert -- null state + Transforming still throws, and the host now turns that throw into a halt
		// at the failed batch instead of an AggregateException out of the save. Nothing is written and
		// nothing is skipped: the batch is re-read on the next invocation.
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted);
		processed.EventsApplied.ShouldBe(0);
		store.Get("order-1").ShouldBeNull();
		read.Freshness.ShouldBe(
			ProjectionFreshness.BoundExpired,
			"a halted projection must not report a write as observed");
	}

	[Fact]
	public async Task HandleDiResolvedImmutableHandler()
	{
		// Arrange -- WhenHandledBy registers the handler into the builder's own collection, which is the
		// container the host resolves from here. That is the production wiring, not a hand-injected one.
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderRecord>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderRecord>>(store);

		var builder = new ImmutableProjectionBuilder<OrderRecord>(harness.Services);
		_ = builder.Async();
		_ = builder.WhenHandledBy<TestOrderPlaced, TestImmutableHandler>();
		builder.Build(harness.Registry);

		var saved = harness.Append(new TestOrderPlaced { Amount = 777m }, "order-1", version: 1);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderRecord>("order-1", saved, safety.Token);

		// Assert
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		read.Projection.ShouldNotBeNull().Total.ShouldBe(777m);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task MergeWithExistingImmutableState()
	{
		// Arrange -- pre-seed the store with the record a prior batch would have left
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderRecord>();
		await store.UpsertAsync("order-1", new OrderRecord(100m, null), safety.Token);
		_ = harness.Services.AddSingleton<IProjectionStore<OrderRecord>>(store);

		var builder = new ImmutableProjectionBuilder<OrderRecord>(harness.Services);
		_ = builder.Async();
		_ = builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(harness.Registry);

		var shipped = DateTimeOffset.UtcNow;
		var saved = harness.Append(
			new TestOrderShipped { ShippedAt = shipped }, "order-1", version: 2);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderRecord>("order-1", saved, safety.Token);

		// Assert -- a new record carrying ShippedAt, Total preserved from the original
		processed.EventsApplied.ShouldBe(1);
		var result = read.Projection.ShouldNotBeNull();
		result.Total.ShouldBe(100m);
		result.ShippedAt.ShouldBe(shipped);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}
}
