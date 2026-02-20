// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="DeadLetterQueueMetrics"/> verifying actual metric instrument behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class DeadLetterQueueMetricsFunctionalShould : IDisposable
{
	private readonly DeadLetterQueueMetrics _metrics = new();
	private readonly MeterListener _listener = new();
	private readonly Dictionary<string, List<(object Value, KeyValuePair<string, object?>[] Tags)>> _recorded = new(StringComparer.Ordinal);

	public DeadLetterQueueMetricsFunctionalShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == DeadLetterQueueMetrics.MeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			RecordMeasurement(instrument.Name, measurement, tags);
		});

		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		_metrics.Dispose();
	}

	[Fact]
	public void UseCorrectMeterName()
	{
		_metrics.Meter.Name.ShouldBe("Excalibur.Dispatch.DeadLetterQueue");
	}

	[Fact]
	public void RecordEnqueued_EmitsCounterWithTypeAndReason()
	{
		_metrics.RecordEnqueued("OrderFailed", "max_retries_exceeded");

		var entries = GetRecorded("dispatch.dlq.enqueued");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries[0];
		((long)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "message_type" && (string)t.Value! == "OrderFailed");
		tags.ShouldContain(t => t.Key == "reason" && (string)t.Value! == "max_retries_exceeded");
	}

	[Fact]
	public void RecordEnqueued_IncludesSourceQueueWhenProvided()
	{
		_metrics.RecordEnqueued("PaymentFailed", "poison_message", sourceQueue: "payments-queue");

		var entries = GetRecorded("dispatch.dlq.enqueued");
		entries.ShouldNotBeEmpty();
		var (_, tags) = entries[0];
		tags.ShouldContain(t => t.Key == "source_queue" && (string)t.Value! == "payments-queue");
	}

	[Fact]
	public void RecordEnqueued_OmitsSourceQueueWhenNull()
	{
		_metrics.RecordEnqueued("Msg", "reason");

		var entries = GetRecorded("dispatch.dlq.enqueued");
		entries.ShouldNotBeEmpty();
		var (_, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && (string)t.Value! == "Msg") &&
			e.Tags.Any(t => t.Key == "reason" && (string)t.Value! == "reason"));
		tags.ShouldNotContain(t => t.Key == "source_queue");
	}

	[Fact]
	public void RecordReplayed_EmitsCounterWithSuccessFlag()
	{
		_metrics.RecordReplayed("OrderRetry", success: true);

		var entries = GetRecorded("dispatch.dlq.replayed");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && (string)t.Value! == "OrderRetry") &&
			e.Tags.Any(t => t.Key == "success" && (bool)t.Value! == true));
		((long)value).ShouldBe(1);
		tags.ShouldContain(t => t.Key == "message_type" && (string)t.Value! == "OrderRetry");
		tags.ShouldContain(t => t.Key == "success" && (bool)t.Value! == true);
	}

	[Fact]
	public void RecordReplayed_TracksFailedReplay()
	{
		_metrics.RecordReplayed("FailedReplay", success: false);

		var entries = GetRecorded("dispatch.dlq.replayed");
		entries.ShouldNotBeEmpty();
		var (_, tags) = entries.First(e =>
			e.Tags.Any(t => t.Key == "message_type" && (string)t.Value! == "FailedReplay") &&
			e.Tags.Any(t => t.Key == "success" && (bool)t.Value! == false));
		tags.ShouldContain(t => t.Key == "success" && (bool)t.Value! == false);
	}

	[Fact]
	public void RecordPurged_EmitsCounterWithBatchCount()
	{
		_metrics.RecordPurged(50, "expired");

		var entries = GetRecorded("dispatch.dlq.purged");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries[0];
		((long)value).ShouldBe(50);
		tags.ShouldContain(t => t.Key == "reason" && (string)t.Value! == "expired");
	}

	[Fact]
	public void UpdateDepth_ReportsViaObservableGauge()
	{
		_metrics.UpdateDepth(42);

		var entries = GetRecorded("dispatch.dlq.depth");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries[0];
		((long)value).ShouldBe(42);
		tags.ShouldContain(t => t.Key == "queue_name" && (string)t.Value! == "default");
	}

	[Fact]
	public void UpdateDepth_TrackNamedQueue()
	{
		_metrics.UpdateDepth(10, "orders-dlq");

		var entries = GetRecorded("dispatch.dlq.depth");
		entries.ShouldNotBeEmpty();
		var (value, tags) = entries[0];
		((long)value).ShouldBe(10);
		tags.ShouldContain(t => t.Key == "queue_name" && (string)t.Value! == "orders-dlq");
	}

	[Fact]
	public void UpdateDepth_OverwritesPreviousValue()
	{
		_metrics.UpdateDepth(100);
		_metrics.UpdateDepth(50);

		var entries = GetRecorded("dispatch.dlq.depth");
		// Observable gauge reports latest value
		entries.ShouldNotBeEmpty();
		var lastEntry = entries[^1];
		((long)lastEntry.Value).ShouldBe(50);
	}

	[Fact]
	public void SupportMeterFactoryConstructor()
	{
		var factory = new TestMeterFactory();
		using var metricsWithFactory = new DeadLetterQueueMetrics(factory);

		metricsWithFactory.Meter.ShouldNotBeNull();
		metricsWithFactory.Meter.Name.ShouldBe(DeadLetterQueueMetrics.MeterName);
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new DeadLetterQueueMetrics((IMeterFactory)null!));
	}

	private void RecordMeasurement(string instrumentName, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
	{
		if (!_recorded.TryGetValue(instrumentName, out var list))
		{
			list = [];
			_recorded[instrumentName] = list;
		}

		list.Add((value, tags.ToArray()));
	}

	private List<(object Value, KeyValuePair<string, object?>[] Tags)> GetRecorded(string instrumentName)
	{
		_listener.RecordObservableInstruments();
		return _recorded.GetValueOrDefault(instrumentName) ?? [];
	}
}
