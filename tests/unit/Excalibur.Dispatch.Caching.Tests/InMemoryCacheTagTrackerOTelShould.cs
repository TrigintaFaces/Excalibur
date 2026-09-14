// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching.Diagnostics;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Rebuilds the OpenTelemetry counter coverage for <see cref="InMemoryCacheTagTracker"/> that was lost
/// when the tracker moved from a key-set contract to per-tag version stamps.
/// </summary>
/// <remarks>
/// <para>
/// The two counters are the only external evidence of what the tracker is doing — how many distinct
/// tags a process has taken on, and how often they are being invalidated. An operator sizing
/// <see cref="CacheOptions.TagTrackerCapacity"/>, or explaining a collapsed hit rate, reads these and
/// nothing else, so a counter that silently stops incrementing is a real defect and not cosmetic.
/// </para>
/// <para>
/// Every arm scopes its <see cref="MetricCollector{T}"/> to a per-test <see cref="TestMeterFactory"/>
/// rather than listening process-wide. The meter name is a fixed constant shared by every tracker
/// instance in the process, so a process-wide listener would see counts from tests running in
/// parallel and the assertions would be flaky by construction.
/// </para>
/// <para>
/// Non-vacuity: each counter arm is paired with an arm proving the counter does NOT move on the
/// neighbouring operation. A tracker that incremented both counters on every call, or one that
/// incremented on the memoized fast path, would satisfy any single arm here and fail the pair.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCacheTagTrackerOTelShould
{
	private const string StampsCreatedCounter = "dispatch.cache.tag_tracker.stamps_created";
	private const string StampsBumpedCounter = "dispatch.cache.tag_tracker.stamps_bumped";
	private const string EpochCollapsesCounter = "dispatch.cache.tag_tracker.epoch_collapses";

	private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

	private static long Total(MetricCollector<long> collector) =>
		collector.GetMeasurementSnapshot().Sum(m => m.Value);

	/// <summary>
	/// An <see cref="IMeterFactory"/> that stamps every meter it creates with itself as the meter
	/// SCOPE, which is what lets <see cref="MetricCollector{T}"/> listen to this test's tracker alone.
	/// </summary>
	/// <remarks>
	/// The shared <c>Tests.Shared.Helpers.TestMeterFactory</c> deliberately does not set a scope, and
	/// the meter NAME here is a fixed production constant shared by every tracker in the process — so a
	/// scopeless collector would also observe trackers built by tests running in parallel, and these
	/// counts would be flaky by construction. Scoping is the isolation; it is not decoration.
	/// </remarks>
	private sealed class ScopedMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Scope = this });
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}

			_meters.Clear();
		}
	}

	// ---- stamps_created ----

	[Fact]
	public async Task RecordStampCreated_WhenATagIsSeenForTheFirstTime()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var created = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsCreatedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		Total(created).ShouldBe(1);
	}

	[Fact]
	public async Task NotRecordStampCreated_WhenAnAlreadyResolvedTagIsResolvedAgain()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var created = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsCreatedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		Total(created).ShouldBe(1,
			"the memoized fast path creates nothing, so counting it would misreport the number of "
			+ "distinct tags the process is tracking — the figure used to size the capacity bound");
	}

	[Fact]
	public async Task RecordOneStampCreatedPerDistinctTag()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var created = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsCreatedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		_ = await tracker.GetOrCreateStampAsync("users", Ct);
		_ = await tracker.GetOrCreateStampAsync("invoices", Ct);

		Total(created).ShouldBe(3);
	}

	/// <summary>
	/// At the capacity bound nothing is stored, so nothing was created. Pins the counter to the
	/// tracker's real state rather than to the number of calls that asked for a stamp.
	/// </summary>
	[Fact]
	public async Task NotRecordStampCreated_ForATagRejectedAtTheCapacityBound()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var created = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsCreatedCounter);
		var tracker = new InMemoryCacheTagTracker(
			meterFactory, MsOptions.Create(new CacheOptions { TagTrackerCapacity = 2 }));

		_ = await tracker.GetOrCreateStampAsync("admitted-1", Ct);
		_ = await tracker.GetOrCreateStampAsync("admitted-2", Ct);
		var countAtCapacity = Total(created);

		_ = await tracker.GetOrCreateStampAsync("rejected-1", Ct);
		_ = await tracker.GetOrCreateStampAsync("rejected-2", Ct);

		countAtCapacity.ShouldBe(2);
		Total(created).ShouldBe(2,
			"a tag rejected at the bound is not stored, so counting it would report more tracked tags "
			+ "than the tracker actually holds");
	}

	// ---- stamps_bumped ----

	[Fact]
	public async Task RecordStampBumped_WhenATagIsInvalidated()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var bumped = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsBumpedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		await tracker.BumpStampAsync("orders", Ct);

		Total(bumped).ShouldBe(1);
	}

	[Fact]
	public async Task RecordEveryBump_IncludingRepeatedBumpsOfTheSameTag()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var bumped = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsBumpedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		await tracker.BumpStampAsync("orders", Ct);
		await tracker.BumpStampAsync("orders", Ct);
		await tracker.BumpStampAsync("orders", Ct);

		Total(bumped).ShouldBe(3,
			"each bump is a real invalidation event; collapsing repeats would hide an invalidation "
			+ "storm, which is the single most useful thing this counter can surface");
	}

	// ---- the two counters must not contaminate each other ----

	[Fact]
	public async Task NotRecordABump_WhenOnlyResolvingStamps()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var bumped = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsBumpedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		_ = await tracker.GetOrCreateStampAsync("users", Ct);

		Total(bumped).ShouldBe(0, "resolving a tag is not an invalidation");
	}

	[Fact]
	public async Task NotRecordACreation_WhenOnlyBumping()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var created = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, StampsCreatedCounter);
		var tracker = new InMemoryCacheTagTracker(meterFactory);

		await tracker.BumpStampAsync("never-resolved", Ct);

		Total(created).ShouldBe(0,
			"a bump replaces a stamp outright and never goes through the creation path");
	}

	// ---- epoch_collapses: the global invalidation is the one an operator most needs to see ----

	[Fact]
	public async Task RecordAnEpochCollapse_WhenABumpAtCapacityGoesGlobal()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var collapses = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, EpochCollapsesCounter);
		var tracker = new InMemoryCacheTagTracker(
			meterFactory, MsOptions.Create(new CacheOptions { TagTrackerCapacity = 2 }));

		_ = await tracker.GetOrCreateStampAsync("a", Ct);
		_ = await tracker.GetOrCreateStampAsync("b", Ct);
		await tracker.BumpStampAsync("untracked-at-capacity", Ct);

		Total(collapses).ShouldBe(1,
			"a collapse invalidates every cached entry at once -- if it is not counted, the miss-rate spike "
			+ "it causes is unexplainable from the outside, and an operator cannot tell a sized-too-small "
			+ "tracker from a genuine invalidation storm");
	}

	[Fact]
	public async Task NotRecordAnEpochCollapse_WhenTheBumpCanBeRecordedPerTag()
	{
		using var meterFactory = new ScopedMeterFactory();
		using var collapses = new MetricCollector<long>(
			meterFactory, DispatchCachingTelemetryConstants.MeterName, EpochCollapsesCounter);
		var tracker = new InMemoryCacheTagTracker(
			meterFactory, MsOptions.Create(new CacheOptions { TagTrackerCapacity = 2 }));

		_ = await tracker.GetOrCreateStampAsync("a", Ct);
		await tracker.BumpStampAsync("a", Ct);          // tracked -> in place
		await tracker.BumpStampAsync("b", Ct);          // untracked but there is room

		Total(collapses).ShouldBe(0,
			"counting an ordinary bump as a collapse would make the signal useless: it is only worth having "
			+ "because it is rare");
	}

	// ---- constructors ----

	[Fact]
	public async Task ParameterlessConstructor_ShouldProduceAWorkingTracker()
	{
		// This overload owns a Meter it creates itself rather than one from a factory, so a
		// factory-scoped collector cannot observe it. What matters here is that the instrument setup
		// does not throw and the tracker still honours its contract.
		var tracker = new InMemoryCacheTagTracker();

		var before = await tracker.GetOrCreateStampAsync("orders", Ct);
		await tracker.BumpStampAsync("orders", Ct);
		var after = await tracker.GetOrCreateStampAsync("orders", Ct);

		before.ShouldNotBeNullOrEmpty();
		after.ShouldNotBe(before);
	}

	[Fact]
	public void MeterFactoryConstructor_ShouldRejectANullFactory()
	{
		_ = Should.Throw<ArgumentNullException>(() => new InMemoryCacheTagTracker(meterFactory: null!));
	}

	[Fact]
	public void OptionsConstructor_ShouldRejectNullArguments()
	{
		using var meterFactory = new ScopedMeterFactory();

		_ = Should.Throw<ArgumentNullException>(
			() => new InMemoryCacheTagTracker(null!, MsOptions.Create(new CacheOptions())));
		_ = Should.Throw<ArgumentNullException>(
			() => new InMemoryCacheTagTracker(meterFactory, null!));
	}
}
