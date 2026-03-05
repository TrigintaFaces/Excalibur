// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="ExemplarHistogramExtensions"/> verifying exemplar tag attachment from Activity.Current.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class ExemplarHistogramExtensionsFunctionalShould : IDisposable
{
	private readonly string _meterName;
	private readonly Meter _meter;
	private readonly MeterListener _listener;
	private readonly List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];

	public ExemplarHistogramExtensionsFunctionalShould()
	{
		_meterName = $"Test.Exemplar.{Guid.NewGuid():N}";
		_meter = new Meter(_meterName);
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
		{
			_measurements.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			_measurements.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		_meter.Dispose();
	}

	[Fact]
	public void HaveCorrectTraceIdTagConstant()
	{
		ExemplarHistogramExtensions.TraceIdTag.ShouldBe("trace_id");
	}

	[Fact]
	public void HaveCorrectSpanIdTagConstant()
	{
		ExemplarHistogramExtensions.SpanIdTag.ShouldBe("span_id");
	}

	[Fact]
	public void RecordDouble_WithExemplarTags_WhenActivityPresent()
	{
		var metricName = $"test.double.exemplar.{Guid.NewGuid():N}";
		var histogram = _meter.CreateHistogram<double>(metricName);

		using var activitySource = new ActivitySource("Test.Exemplar.Source");
		using var activityListener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Test.Exemplar.Source",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(activityListener);

		using var activity = activitySource.StartActivity("test-operation");
		activity.ShouldNotBeNull();

		var tags = new TagList { { "custom", "value" } };
		histogram.RecordWithExemplar(42.5, tags);

		var recorded = _measurements.FirstOrDefault(m => m.Name == metricName);
		recorded.Name.ShouldNotBeNull();
		((double)recorded.Value).ShouldBe(42.5);

		// Should contain trace_id and span_id from active activity
		recorded.Tags.ShouldContain(t => t.Key == "trace_id");
		recorded.Tags.ShouldContain(t => t.Key == "span_id");
		recorded.Tags.ShouldContain(t => t.Key == "custom" && (string)t.Value! == "value");
	}

	[Fact]
	public void RecordDouble_WithoutExemplarTags_WhenNoActivity()
	{
		var metricName = $"test.double.no-activity.{Guid.NewGuid():N}";
		var histogram = _meter.CreateHistogram<double>(metricName);

		// Ensure no current activity
		Activity.Current = null;

		var tags = new TagList { { "operation", "test" } };
		histogram.RecordWithExemplar(10.0, tags);

		var recorded = _measurements.FirstOrDefault(m => m.Name == metricName);
		recorded.Name.ShouldNotBeNull();

		// Should NOT contain trace_id/span_id
		recorded.Tags.ShouldNotContain(t => t.Key == "trace_id");
		recorded.Tags.ShouldNotContain(t => t.Key == "span_id");
	}

	[Fact]
	public void RecordDouble_WithMessageType()
	{
		var metricName = $"test.double.message-type.{Guid.NewGuid():N}";
		var histogram = _meter.CreateHistogram<double>(metricName);

		Activity.Current = null;

		histogram.RecordWithExemplar(100.0, "OrderCreated", true);

		var recorded = _measurements.FirstOrDefault(m => m.Name == metricName);
		recorded.Name.ShouldNotBeNull();
		((double)recorded.Value).ShouldBe(100.0);
		recorded.Tags.ShouldContain(t => t.Key == "message_type" && (string)t.Value! == "OrderCreated");
		recorded.Tags.ShouldContain(t => t.Key == "success" && (bool)t.Value! == true);
	}

	[Fact]
	public void RecordDouble_WithActivityContext()
	{
		var metricName = $"test.double.activity-context.{Guid.NewGuid():N}";
		var histogram = _meter.CreateHistogram<double>(metricName);
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);

		var tags = new TagList();
		histogram.RecordWithExemplar(25.0, activityContext, tags);

		var recorded = _measurements.FirstOrDefault(m => m.Name == metricName);
		recorded.Name.ShouldNotBeNull();
		recorded.Tags.ShouldContain(t => t.Key == "trace_id" && (string)t.Value! == traceId.ToString());
		recorded.Tags.ShouldContain(t => t.Key == "span_id" && (string)t.Value! == spanId.ToString());
	}

	[Fact]
	public void RecordDouble_WithDefaultActivityContext_NoExemplarTags()
	{
		var metricName = $"test.double.default-context.{Guid.NewGuid():N}";
		var histogram = _meter.CreateHistogram<double>(metricName);

		var tags = new TagList();
		histogram.RecordWithExemplar(50.0, default, tags);

		var recorded = _measurements.FirstOrDefault(m => m.Name == metricName);
		recorded.Name.ShouldNotBeNull();
		recorded.Tags.ShouldNotContain(t => t.Key == "trace_id");
	}

	[Fact]
	public void RecordLong_WithExemplarTags_WhenActivityPresent()
	{
		var metricName = $"test.long.exemplar.{Guid.NewGuid():N}";
		var histogram = _meter.CreateHistogram<long>(metricName);

		using var activitySource = new ActivitySource("Test.Exemplar.Source.Long");
		using var activityListener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Test.Exemplar.Source.Long",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(activityListener);

		using var activity = activitySource.StartActivity("test-long-op");
		activity.ShouldNotBeNull();

		var tags = new TagList { { "type", "long" } };
		histogram.RecordWithExemplar(100L, tags);

		var recorded = _measurements.FirstOrDefault(m => m.Name == metricName);
		recorded.Name.ShouldNotBeNull();
		((long)recorded.Value).ShouldBe(100L);
		recorded.Tags.ShouldContain(t => t.Key == "trace_id");
		recorded.Tags.ShouldContain(t => t.Key == "span_id");
	}

	[Fact]
	public void ThrowOnNullHistogram_Double_TagList()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(null!, 1.0, new TagList()));
	}

	[Fact]
	public void ThrowOnNullHistogram_Double_MessageType()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(null!, 1.0, "type", true));
	}

	[Fact]
	public void ThrowOnNullHistogram_Double_ActivityContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(null!, 1.0, default, new TagList()));
	}

	[Fact]
	public void ThrowOnNullHistogram_Long()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar((Histogram<long>)null!, 1L, new TagList()));
	}
}
