// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Independent engage-lock (nbannx, author≠impl) for <see cref="InMemoryCdcIdempotencyFilter"/>'s capacity
/// fail-closed contract AND its degradation telemetry. Distinct from the coupled unit test: this lock also
/// asserts the cross-module degradation counter <c>excalibur.cdc.idempotency.capacity_exceeded</c> increments,
/// proving the alertable signal actually fires (a counter nobody observes is not an observability control).
/// </summary>
/// <remarks>
/// <b>RED on the pre-fix surface:</b> previously a not-yet-seen event at capacity returned <c>false</c>
/// (silent skip-when-full) and emitted no signal — the handler ran un-tracked and a redelivery could
/// double-process. Now <see cref="InMemoryCdcIdempotencyFilter.IsProcessedAsync"/> throws a transient
/// <see cref="CdcIdempotencyCapacityExceededException"/> (fail closed) and increments the counter.
/// Deterministic: no timing — the counter is read via a scoped <see cref="MeterListener"/>.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcIdempotencyCapacityFailClosedEngageShould
{
	/// <summary>The asking consumer. These arms are not about consumer isolation, so one identity is used
	/// throughout; CdcIdempotencyConsumerCollisionShould is where two identities are contrasted.</summary>
	private const string TestConsumer = "test-consumer";

	private const string CapacityExceededMetric = "excalibur.cdc.idempotency.capacity_exceeded";
	private static readonly byte[] SeqVal = [0x00, 0x01];
	private const string Table = "dbo_Orders";

	[Fact]
	public async Task FailClosedAndIncrementCounter_WhenIsProcessedSeesNewEvent_AtCapacity()
	{
		// Arrange — a filter at capacity (2 tracked events).
		var filter = new InMemoryCdcIdempotencyFilter(2, NullLogger<InMemoryCdcIdempotencyFilter>.Instance);
		await filter.MarkProcessedAsync(Table, [0x01], SeqVal, TestConsumer, CancellationToken.None);
		await filter.MarkProcessedAsync(Table, [0x02], SeqVal, TestConsumer, CancellationToken.None);

		var observed = 0L;
		using var listener = CreateCounterListener(v => observed += v);
		listener.Start();

		// Act & Assert — a not-yet-seen event at capacity fails closed (transient) BEFORE the handler runs.
		await Should.ThrowAsync<CdcIdempotencyCapacityExceededException>(
			() => filter.IsProcessedAsync(Table, [0xFE], SeqVal, TestConsumer, CancellationToken.None));

		listener.Dispose(); // flush pending measurements
		observed.ShouldBeGreaterThanOrEqualTo(
			1,
			"the fail-closed pre-process gate must emit the alertable capacity_exceeded degradation signal");
	}

	[Fact]
	public async Task IncrementCounterButNotThrow_WhenMarkProcessedHitsCapacity()
	{
		// Arrange — at capacity (2 tracked events).
		var filter = new InMemoryCdcIdempotencyFilter(2, NullLogger<InMemoryCdcIdempotencyFilter>.Instance);
		await filter.MarkProcessedAsync(Table, [0x01], SeqVal, TestConsumer, CancellationToken.None);
		await filter.MarkProcessedAsync(Table, [0x02], SeqVal, TestConsumer, CancellationToken.None);

		var observed = 0L;
		using var listener = CreateCounterListener(v => observed += v);
		listener.Start();

		// Act & Assert — MarkProcessedAsync runs AFTER the handler, so at capacity it must NEVER throw; it
		// still emits the degradation signal and leaves the bounded set untouched.
		await Should.NotThrowAsync(
			() => filter.MarkProcessedAsync(Table, [0xFF], SeqVal, TestConsumer, CancellationToken.None));
		filter.Count.ShouldBe(2);

		listener.Dispose();
		observed.ShouldBeGreaterThanOrEqualTo(
			1,
			"a best-effort MarkProcessed at capacity must still emit the capacity_exceeded degradation signal");
	}

	private static MeterListener CreateCounterListener(Action<long> record)
	{
		var listener = new MeterListener
		{
			InstrumentPublished = (instrument, l) =>
			{
				if (instrument.Name == CapacityExceededMetric)
				{
					l.EnableMeasurementEvents(instrument);
				}
			},
		};
		listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => record(measurement));
		return listener;
	}
}
