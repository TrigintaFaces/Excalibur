// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// WIRE lock (ADR-336 advertised-but-unwired) for the exactly-once dedup meter.
/// Proves that an OpenTelemetry <see cref="MeterProvider"/> built via the framework's
/// turnkey registration (<see cref="OpenTelemetryExtensions.AddAllDispatchMetrics(MeterProviderBuilder)"/>,
/// the same path <c>AddDispatchInstrumentation()</c> uses) actually <b>collects</b> a measurement
/// emitted on the <c>Excalibur.Dispatch.ExactlyOnce</c> meter — i.e. the consumer resolves and
/// reads-through the meter end-to-end, not merely that the name string is present in the array.
/// </summary>
/// <remarks>
/// The counter <c>dispatch.exactlyonce.duplicates.suppressed</c> is emitted by
/// <c>InMemoryDeduplicator</c> on meter <c>Excalibur.Dispatch.ExactlyOnce</c>
/// (<c>DispatchTelemetryConstants.Meters.ExactlyOnce</c>). If that meter is absent from
/// <c>AllMeterNames</c> (the pre-gnjip4 state), the MeterProvider never subscribes to it and the
/// in-memory exporter collects nothing for it — this test fails. That is its non-vacuity guarantee.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "OpenTelemetry")]
public sealed class ExactlyOnceMeterCollectionShould
{
	private const string ExactlyOnceMeterName = "Excalibur.Dispatch.ExactlyOnce";

	[Fact]
	public void CollectTheExactlyOnceSuppressedCounter_ThroughTheTurnkeyRegistration()
	{
		// Flakiness guard (r0oidw / bd-sbu1mf class): the framework meter name is process-global, so
		// under the AsyncRisk superset shard a PARALLEL emission of the production counter
		// 'dispatch.exactlyonce.duplicates.suppressed' (from another assembly's InMemoryDeduplicator/
		// test) lands in THIS provider's collection window and inflates the Sum. Subscription is by
		// METER name, not instrument — so we keep the real meter name (the wiring under test) but use a
		// UNIQUE instrument name per invocation, isolating this assertion from cross-test contamination
		// while preserving the WIRE proof (a subscribed meter still reads through the measurement).
		var suppressedCounterName = $"dispatch.exactlyonce.duplicates.suppressed.test-{Guid.NewGuid():N}";
		// Arrange — build a REAL OTel MeterProvider via the production registration path
		// (AddAllDispatchMetrics -> AddMeter(AllMeterNames)). This is the exact call
		// AddDispatchInstrumentation()/AddAllDispatchMetrics() route consumers through.
		var exporter = new ListMetricExporter();

		using var meterProvider = Sdk.CreateMeterProviderBuilder()
			.AddAllDispatchMetrics()
			// Determinism guard (vwip93): AddAllDispatchMetrics subscribes EVERY dispatch meter, so under the
			// AsyncRisk superset shard a parallel flood of emissions on those shared, process-global meters can
			// land in this provider's collection window (and can exhaust the SDK's metric-stream table before
			// our instrument is even published). Drop every instrument except this invocation's unique counter
			// so the provider only ever materializes ONE metric stream — immune to cross-test contamination and
			// stream-table exhaustion. This does NOT weaken the WIRE proof: the meter is still subscribed via
			// the turnkey path, so if 'Excalibur.Dispatch.ExactlyOnce' were absent from AllMeterNames our
			// counter would never be collected regardless of the view (non-vacuity preserved).
			.AddView(instrument =>
				instrument.Name == suppressedCounterName ? null : MetricStreamConfiguration.Drop)
			.AddReader(new BaseExportingMetricReader(exporter))
			.Build();

		meterProvider.ShouldNotBeNull();

		// Act — emit a real measurement on the exactly-once meter, exactly as InMemoryDeduplicator does.
		// Create the meter/instrument AFTER Build() so the provider's InstrumentPublished listener
		// subscribes it (this is the read-through: only a SUBSCRIBED meter is collected).
		using var meter = new Meter(ExactlyOnceMeterName);
		var suppressedCounter = meter.CreateCounter<long>(suppressedCounterName);
		suppressedCounter.Add(1);

		// Force the reader to collect + export.
		meterProvider.ForceFlush(5000).ShouldBeTrue("the MeterProvider should flush within the timeout");

		// Assert — the exported metrics MUST contain the exactly-once meter's instrument with the value.
		// If Excalibur.Dispatch.ExactlyOnce were not in AllMeterNames, the provider would not have
		// subscribed to it and this metric would be absent (non-vacuity).
		var suppressed = exporter.Collected.SingleOrDefault(m =>
			m.MeterName == ExactlyOnceMeterName && m.InstrumentName == suppressedCounterName);

		suppressed.ShouldNotBe(default,
			$"The turnkey OTel registration did not collect the '{suppressedCounterName}' counter on meter " +
			$"'{ExactlyOnceMeterName}'. This means AddAllDispatchMetrics()/AddDispatchInstrumentation() is not " +
			"subscribed to the exactly-once meter — the dedup counter is advertised but unwired.");

		// The recorded value must actually be collected (read-through of the measurement, not just the name).
		suppressed.Sum.ShouldBe(1,
			"the collected exactly-once suppressed-counter value must match the emitted measurement");
	}

	/// <summary>
	/// Minimal in-memory metric exporter — records the (meter, instrument, summed value) of every
	/// collected metric point. Avoids taking a dependency on the InMemory exporter package.
	/// </summary>
	private sealed class ListMetricExporter : BaseExporter<Metric>
	{
		public List<(string MeterName, string InstrumentName, long Sum)> Collected { get; } = [];

		public override ExportResult Export(in Batch<Metric> batch)
		{
			foreach (var metric in batch)
			{
				long sum = 0;
				foreach (ref readonly var point in metric.GetMetricPoints())
				{
					sum += point.GetSumLong();
				}

				Collected.Add((metric.MeterName, metric.Name, sum));
			}

			return ExportResult.Success;
		}
	}
}
