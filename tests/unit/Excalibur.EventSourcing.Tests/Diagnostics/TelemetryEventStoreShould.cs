// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TelemetryEventStore"/>.
/// Verifies that the telemetry decorator correctly instruments all event store operations
/// with OpenTelemetry metrics (counters, histograms) and distributed tracing (activities).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TelemetryEventStoreShould : IDisposable
{
	private readonly IEventStore _innerStore;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetryEventStore _sut;
	private readonly ActivityListener _listener;
	private readonly List<Activity> _capturedActivities = [];
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterMeasurements = [];
	private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _histogramMeasurements = [];

	private static readonly string ProviderName = "test-provider";

	public TelemetryEventStoreShould()
	{
		_innerStore = A.Fake<IEventStore>();
		_meter = new Meter("TelemetryEventStoreShould.Tests", "1.0.0");
		_activitySource = new ActivitySource("TelemetryEventStoreShould.Tests");

		// Set up activity listener to capture activities
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == _activitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _capturedActivities.Add(activity)
		};
		ActivitySource.AddActivityListener(_listener);

		// Set up meter listener to capture measurements
		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter == _meter)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			_counterMeasurements.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
		{
			_histogramMeasurements.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_meterListener.Start();

		_sut = new TelemetryEventStore(_innerStore, _meter, _activitySource, ProviderName);
	}

	public void Dispose()
	{
		_meterListener.Dispose();
		_listener.Dispose();
		foreach (var activity in _capturedActivities)
		{
			activity.Dispose();
		}
		_activitySource.Dispose();
		_meter.Dispose();
	}

	#region Constructor Validation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenInnerStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(null!, _meter, _activitySource, ProviderName));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMeterIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(_innerStore, null!, _activitySource, ProviderName));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenActivitySourceIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(_innerStore, _meter, null!, ProviderName));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenProviderNameIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(_innerStore, _meter, _activitySource, null!));
	}

	#endregion

	#region LoadAsync (full load) Tests

	[Fact]
	public async Task DelegateLoadAsyncToInnerStore()
	{
		var expectedEvents = CreateStoredEvents(3);
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expectedEvents));

		var result = await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);

		result.ShouldBe(expectedEvents);
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordSuccessMetrics_WhenLoadAsyncSucceeds()
	{
		var events = CreateStoredEvents(2);
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Value == 1 &&
			m.Tags.Any(t => t.Key == EventSourcingTags.Operation && (string?)t.Value == "load") &&
			m.Tags.Any(t => t.Key == EventSourcingTags.Provider && (string?)t.Value == ProviderName) &&
			m.Tags.Any(t => t.Key == EventSourcingTags.OperationResult && (string?)t.Value == EventSourcingTagValues.Success) &&
			m.Tags.Any(t => t.Key == EventSourcingTags.AggregateType && (string?)t.Value == "Order"));

		_histogramMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreDuration &&
			m.Value >= 0);
	}

	[Fact]
	public async Task CreateActivity_WhenLoadAsyncIsCalled()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(1)));

		await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);

		var activity = _capturedActivities.ShouldHaveSingleItem();
		activity.OperationName.ShouldBe(EventSourcingActivities.Load);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-1");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Order");
		activity.GetTagItem(EventSourcingTags.Provider).ToString().ShouldBe(ProviderName);
		activity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(1);
	}

	[Fact]
	public async Task RecordFailureMetrics_WhenLoadAsyncThrows()
	{
		var exception = new InvalidOperationException("Test load failure");
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.ThrowsAsync(exception);

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.LoadAsync("agg-1", "Order", CancellationToken.None));

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.OperationResult && (string?)t.Value == EventSourcingTagValues.Failure));

		_histogramMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreDuration &&
			m.Value >= 0);
	}

	[Fact]
	public async Task SetActivityErrorStatus_WhenLoadAsyncThrows()
	{
		var exception = new InvalidOperationException("Test load failure");
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.ThrowsAsync(exception);

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.LoadAsync("agg-1", "Order", CancellationToken.None));

		var activity = _capturedActivities.ShouldHaveSingleItem();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("Test load failure");
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
	}

	#endregion

	#region LoadAsync (from version) Tests

	[Fact]
	public async Task DelegateLoadAsyncFromVersionToInnerStore()
	{
		var expectedEvents = CreateStoredEvents(2);
		A.CallTo(() => _innerStore.LoadAsync("agg-2", "Customer", 5L, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expectedEvents));

		var result = await _sut.LoadAsync("agg-2", "Customer", 5L, CancellationToken.None);

		result.ShouldBe(expectedEvents);
		A.CallTo(() => _innerStore.LoadAsync("agg-2", "Customer", 5L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordSuccessMetrics_WhenLoadAsyncFromVersionSucceeds()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-2", "Customer", 5L, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(1)));

		await _sut.LoadAsync("agg-2", "Customer", 5L, CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.Operation && (string?)t.Value == "load") &&
			m.Tags.Any(t => t.Key == EventSourcingTags.OperationResult && (string?)t.Value == EventSourcingTagValues.Success));
	}

	[Fact]
	public async Task CreateActivity_WithFromVersionTag_WhenLoadAsyncFromVersionIsCalled()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-2", "Customer", 10L, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(2)));

		await _sut.LoadAsync("agg-2", "Customer", 10L, CancellationToken.None);

		var activity = _capturedActivities.ShouldHaveSingleItem();
		activity.OperationName.ShouldBe(EventSourcingActivities.Load);
		activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBe(10L);
		activity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(2);
	}

	[Fact]
	public async Task RecordFailureMetrics_WhenLoadAsyncFromVersionThrows()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-2", "Customer", 5L, A<CancellationToken>._))
			.ThrowsAsync(new TimeoutException("Timed out"));

		await Should.ThrowAsync<TimeoutException>(async () =>
			await _sut.LoadAsync("agg-2", "Customer", 5L, CancellationToken.None));

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.OperationResult && (string?)t.Value == EventSourcingTagValues.Failure));
	}

	#endregion

	#region AppendAsync Tests

	[Fact]
	public async Task DelegateAppendAsyncToInnerStore()
	{
		var events = new List<IDomainEvent> { A.Fake<IDomainEvent>() };
		var expectedResult = AppendResult.CreateSuccess(2, 100);
		A.CallTo(() => _innerStore.AppendAsync("agg-3", "Order", events, 1L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(expectedResult));

		var result = await _sut.AppendAsync("agg-3", "Order", events, 1L, CancellationToken.None);

		result.ShouldBe(expectedResult);
		A.CallTo(() => _innerStore.AppendAsync("agg-3", "Order", events, 1L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordSuccessMetrics_WhenAppendAsyncSucceeds()
	{
		var events = new List<IDomainEvent> { A.Fake<IDomainEvent>() };
		A.CallTo(() => _innerStore.AppendAsync("agg-3", "Order", events, 1L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(AppendResult.CreateSuccess(2, 100)));

		await _sut.AppendAsync("agg-3", "Order", events, 1L, CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.Operation && (string?)t.Value == "append") &&
			m.Tags.Any(t => t.Key == EventSourcingTags.Provider && (string?)t.Value == ProviderName) &&
			m.Tags.Any(t => t.Key == EventSourcingTags.OperationResult && (string?)t.Value == EventSourcingTagValues.Success) &&
			m.Tags.Any(t => t.Key == EventSourcingTags.AggregateType && (string?)t.Value == "Order"));

		_histogramMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreDuration &&
			m.Value >= 0);
	}

	[Fact]
	public async Task CreateActivity_WithExpectedVersionTag_WhenAppendAsyncIsCalled()
	{
		var events = new List<IDomainEvent> { A.Fake<IDomainEvent>() };
		A.CallTo(() => _innerStore.AppendAsync("agg-3", "Order", events, 5L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(AppendResult.CreateSuccess(6, 200)));

		await _sut.AppendAsync("agg-3", "Order", events, 5L, CancellationToken.None);

		var activity = _capturedActivities.ShouldHaveSingleItem();
		activity.OperationName.ShouldBe(EventSourcingActivities.Append);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-3");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Order");
		activity.GetTagItem(EventSourcingTags.ExpectedVersion).ShouldBe(5L);
		activity.GetTagItem(EventSourcingTags.Provider).ToString().ShouldBe(ProviderName);
	}

	[Fact]
	public async Task RecordFailureMetrics_WhenAppendAsyncThrows()
	{
		var events = new List<IDomainEvent> { A.Fake<IDomainEvent>() };
		A.CallTo(() => _innerStore.AppendAsync("agg-3", "Order", events, 1L, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Concurrency failure"));

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.AppendAsync("agg-3", "Order", events, 1L, CancellationToken.None));

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.Operation && (string?)t.Value == "append") &&
			m.Tags.Any(t => t.Key == EventSourcingTags.OperationResult && (string?)t.Value == EventSourcingTagValues.Failure));
	}

	[Fact]
	public async Task SetActivityErrorStatus_WhenAppendAsyncThrows()
	{
		var events = new List<IDomainEvent> { A.Fake<IDomainEvent>() };
		var exception = new InvalidOperationException("Append failed");
		A.CallTo(() => _innerStore.AppendAsync("agg-3", "Order", events, 1L, A<CancellationToken>._))
			.ThrowsAsync(exception);

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.AppendAsync("agg-3", "Order", events, 1L, CancellationToken.None));

		var activity = _capturedActivities.ShouldHaveSingleItem();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("Append failed");
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
	}

	#endregion

	#region Duration Recording Tests

	[Fact]
	public async Task RecordNonNegativeDuration_ForLoadAndAppendOperations()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(0)));
		A.CallTo(() => _innerStore.AppendAsync("agg-1", "Order", A<IEnumerable<IDomainEvent>>._, 0L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(AppendResult.CreateSuccess(1, 1)));

		await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);
		await _sut.AppendAsync("agg-1", "Order", Array.Empty<IDomainEvent>(), 0L, CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_histogramMeasurements.Count.ShouldBe(2);
		_histogramMeasurements.ShouldAllBe(m =>
			m.Name == EventSourcingMetricNames.EventStoreDuration && m.Value >= 0);
	}

	#endregion

	#region TagCardinalityGuard Tests

	[Fact]
	public async Task UseTagCardinalityGuard_ToLimitAggregateTypeDimension()
	{
		for (var i = 0; i < 128; i++)
		{
			A.CallTo(() => _innerStore.LoadAsync(A<string>._, A<string>._, A<CancellationToken>._))
				.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(0)));

			await _sut.LoadAsync("agg-1", $"Type{i}", CancellationToken.None);
		}

		_counterMeasurements.Clear();
		_histogramMeasurements.Clear();

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "OverflowType", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(0)));

		await _sut.LoadAsync("agg-1", "OverflowType", CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.AggregateType && (string?)t.Value == "__other__"));
	}

	[Fact]
	public async Task AllowKnownAggregateType_WithinCardinalityLimit()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(1)));

		await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.AggregateType && (string?)t.Value == "Order"));

		_counterMeasurements.ShouldNotContain(m =>
			m.Name == EventSourcingMetricNames.EventStoreOperations &&
			m.Tags.Any(t => t.Key == EventSourcingTags.AggregateType && (string?)t.Value == "__other__"));
	}

	#endregion

	#region Exception Propagation Tests

	[Fact]
	public async Task PropagateException_WhenLoadAsyncThrows()
	{
		var expectedException = new InvalidOperationException("Original exception");
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.ThrowsAsync(expectedException);

		var actualException = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.LoadAsync("agg-1", "Order", CancellationToken.None));

		actualException.ShouldBeSameAs(expectedException);
	}

	[Fact]
	public async Task PropagateException_WhenAppendAsyncThrows()
	{
		var events = new List<IDomainEvent> { A.Fake<IDomainEvent>() };
		var expectedException = new InvalidOperationException("Append exception");
		A.CallTo(() => _innerStore.AppendAsync("agg-1", "Order", events, 0L, A<CancellationToken>._))
			.ThrowsAsync(expectedException);

		var actualException = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.AppendAsync("agg-1", "Order", events, 0L, CancellationToken.None));

		actualException.ShouldBeSameAs(expectedException);
	}

	#endregion

	#region Provider Tag Tests

	[Fact]
	public async Task IncludeProviderTag_InAllSuccessMetrics()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(0)));

		await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);
		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldAllBe(m =>
			m.Tags.Any(t => t.Key == EventSourcingTags.Provider && (string?)t.Value == ProviderName));

		_histogramMeasurements.ShouldAllBe(m =>
			m.Tags.Any(t => t.Key == EventSourcingTags.Provider && (string?)t.Value == ProviderName));
	}

	[Fact]
	public async Task IncludeProviderTag_InAllFailureMetrics()
	{
		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("fail"));

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.LoadAsync("agg-1", "Order", CancellationToken.None));

		_meterListener.RecordObservableInstruments();

		_counterMeasurements.ShouldAllBe(m =>
			m.Tags.Any(t => t.Key == EventSourcingTags.Provider && (string?)t.Value == ProviderName));

		_histogramMeasurements.ShouldAllBe(m =>
			m.Tags.Any(t => t.Key == EventSourcingTags.Provider && (string?)t.Value == ProviderName));
	}

	#endregion

	#region Activity Without Listener Tests

	[Fact]
	public async Task NotThrow_WhenNoActivityListenerAttached()
	{
		using var unlistenedSource = new ActivitySource("TelemetryEventStoreShould.NoListener");
		using var meter = new Meter("TelemetryEventStoreShould.NoListener");
		var store = new TelemetryEventStore(_innerStore, meter, unlistenedSource, "no-listener");

		A.CallTo(() => _innerStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(CreateStoredEvents(0)));

		await Should.NotThrowAsync(async () =>
			await store.LoadAsync("agg-1", "Order", CancellationToken.None));
	}

	#endregion

	#region Helper Methods

	private static IReadOnlyList<StoredEvent> CreateStoredEvents(int count)
	{
		var events = new List<StoredEvent>();
		for (var i = 0; i < count; i++)
		{
			events.Add(new StoredEvent(
				EventId: $"evt-{i}",
				AggregateId: "agg-1",
				AggregateType: "TestAggregate",
				EventType: "TestEvent",
				EventData: [],
				Metadata: null,
				Version: i + 1,
				Timestamp: DateTimeOffset.UtcNow));
		}
		return events;
	}

	#endregion
}
