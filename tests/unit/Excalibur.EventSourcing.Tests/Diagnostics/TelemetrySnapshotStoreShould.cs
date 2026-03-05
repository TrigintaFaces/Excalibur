// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TelemetrySnapshotStore"/>.
/// Validates metric recording, duration histograms, activity tracing, provider tagging,
/// delegation to the inner store, and error-path behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TelemetrySnapshotStoreShould : IDisposable
{
	private readonly ISnapshotStore _innerFake;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetrySnapshotStore _sut;
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterRecordings = [];
	private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _histogramRecordings = [];
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _capturedActivities = [];

	private const string ProviderName = "test-provider";
	private const string AggregateId = "order-123";
	private const string AggregateType = "Order";

	public TelemetrySnapshotStoreShould()
	{
		_innerFake = A.Fake<ISnapshotStore>();

		_meter = new Meter("Excalibur.EventSourcing.SnapshotStore.Test." + Guid.NewGuid().ToString("N")[..8]);
		_activitySource = new ActivitySource("Excalibur.EventSourcing.SnapshotStore.Test." + Guid.NewGuid().ToString("N")[..8]);

		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter == _meter)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
		{
			_counterRecordings.Add((instrument.Name, value, tags.ToArray()));
		});
		_meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
		{
			_histogramRecordings.Add((instrument.Name, value, tags.ToArray()));
		});
		_meterListener.Start();

		_activityListener = new ActivityListener
		{
			ShouldListenTo = source => source == _activitySource,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _capturedActivities.Add(activity),
		};
		ActivitySource.AddActivityListener(_activityListener);

		_sut = new TelemetrySnapshotStore(_innerFake, _meter, _activitySource, ProviderName);
	}

	public void Dispose()
	{
		_activityListener.Dispose();
		foreach (var activity in _capturedActivities)
		{
			activity.Dispose();
		}

		_meterListener.Dispose();
		_meter.Dispose();
		_activitySource.Dispose();
	}

	// ------------------------------------------------------------------
	// Constructor validation
	// ------------------------------------------------------------------

	[Fact]
	public void Throw_When_Inner_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(null!, _meter, _activitySource, ProviderName));
	}

	[Fact]
	public void Throw_When_Meter_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(_innerFake, null!, _activitySource, ProviderName));
	}

	[Fact]
	public void Throw_When_ActivitySource_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(_innerFake, _meter, null!, ProviderName));
	}

	[Fact]
	public void Throw_When_ProviderName_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(_innerFake, _meter, _activitySource, null!));
	}

	// ------------------------------------------------------------------
	// GetLatestSnapshotAsync — success path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Delegate_GetLatestSnapshotAsync_To_Inner()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));

		// Act
		var result = await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(snapshot);
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Record_Operations_Counter_On_GetLatestSnapshotAsync_Success()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));

		// Act
		await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Value.ShouldBe(1);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Operation && (string)t.Value! == "get_snapshot");
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Provider && (string)t.Value! == ProviderName);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Success);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.AggregateType && (string)t.Value! == AggregateType);
	}

	[Fact]
	public async Task Record_Duration_Histogram_On_GetLatestSnapshotAsync_Success()
	{
		// Arrange
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));

		// Act
		await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreDuration)
			.ToList();
		durations.ShouldNotBeEmpty();
		durations[^1].Value.ShouldBeGreaterThanOrEqualTo(0);
		durations[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Operation && (string)t.Value! == "get_snapshot");
	}

	[Fact]
	public async Task Set_NotFound_Activity_Tag_When_GetLatestSnapshotAsync_Returns_Null()
	{
		// Arrange
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));

		// Act
		await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.GetSnapshot);
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.OperationResult).ToString().ShouldBe(EventSourcingTagValues.NotFound);
	}

	[Fact]
	public async Task Set_Success_Activity_Tag_When_GetLatestSnapshotAsync_Returns_Snapshot()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));

		// Act
		await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.GetSnapshot);
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.OperationResult).ToString().ShouldBe(EventSourcingTagValues.Success);
	}

	// ------------------------------------------------------------------
	// GetLatestSnapshotAsync — error path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Record_Failure_Counter_On_GetLatestSnapshotAsync_Error()
	{
		// Arrange
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Throws(new InvalidOperationException("get error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None).AsTask());

		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Failure);
	}

	[Fact]
	public async Task Set_Error_Activity_Status_On_GetLatestSnapshotAsync_Error()
	{
		// Arrange
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Throws(new InvalidOperationException("trace get error"));

		// Act
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None).AsTask());

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.GetSnapshot);
		activity.ShouldNotBeNull();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("trace get error");
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
	}

	// ------------------------------------------------------------------
	// SaveSnapshotAsync — success path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Delegate_SaveSnapshotAsync_To_Inner()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot();
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Throw_When_SaveSnapshotAsync_Receives_Null_Snapshot()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task Record_Operations_Counter_On_SaveSnapshotAsync_Success()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot();
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Value.ShouldBe(1);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Operation && (string)t.Value! == "save_snapshot");
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Provider && (string)t.Value! == ProviderName);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task Record_Duration_Histogram_On_SaveSnapshotAsync_Success()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot();
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreDuration)
			.ToList();
		durations.ShouldNotBeEmpty();
		durations[^1].Value.ShouldBeGreaterThanOrEqualTo(0);
		durations[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Operation && (string)t.Value! == "save_snapshot");
	}

	[Fact]
	public async Task Set_Version_Activity_Tag_On_SaveSnapshotAsync()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot(version: 42);
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.SaveSnapshot);
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.Version).ShouldBe(42L);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe(AggregateId);
		activity.GetTagItem(EventSourcingTags.Provider).ToString().ShouldBe(ProviderName);
	}

	// ------------------------------------------------------------------
	// SaveSnapshotAsync — error path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Record_Failure_Counter_On_SaveSnapshotAsync_Error()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot();
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Throws(new InvalidOperationException("save error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());

		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Failure);
	}

	[Fact]
	public async Task Set_Error_Activity_Status_On_SaveSnapshotAsync_Error()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot();
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Throws(new InvalidOperationException("trace save error"));

		// Act
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.SaveSnapshot);
		activity.ShouldNotBeNull();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("trace save error");
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
	}

	// ------------------------------------------------------------------
	// DeleteSnapshotsAsync — success path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Delegate_DeleteSnapshotsAsync_To_Inner()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Record_Operations_Counter_On_DeleteSnapshotsAsync_Success()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Value.ShouldBe(1);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Operation && (string)t.Value! == "delete_snapshots");
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Provider && (string)t.Value! == ProviderName);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task Record_Duration_Histogram_On_DeleteSnapshotsAsync_Success()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreDuration)
			.ToList();
		durations.ShouldNotBeEmpty();
		durations[^1].Value.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task Start_DeleteSnapshots_Activity_With_Correct_Tags()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.DeleteSnapshots);
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe(AggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe(AggregateType);
		activity.GetTagItem(EventSourcingTags.Provider).ToString().ShouldBe(ProviderName);
	}

	// ------------------------------------------------------------------
	// DeleteSnapshotsAsync — error path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Record_Failure_Counter_On_DeleteSnapshotsAsync_Error()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Throws(new InvalidOperationException("delete error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None).AsTask());

		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Failure);
	}

	[Fact]
	public async Task Set_Error_Activity_Status_On_DeleteSnapshotsAsync_Error()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Throws(new InvalidOperationException("trace delete error"));

		// Act
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None).AsTask());

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.DeleteSnapshots);
		activity.ShouldNotBeNull();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
	}

	// ------------------------------------------------------------------
	// DeleteSnapshotsOlderThanAsync — success path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Delegate_DeleteSnapshotsOlderThanAsync_To_Inner()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 10L, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 10L, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 10L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Record_Operations_Counter_On_DeleteSnapshotsOlderThanAsync_Success()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Value.ShouldBe(1);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Operation && (string)t.Value! == "delete_snapshots_older");
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.Provider && (string)t.Value! == ProviderName);
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Success);
	}

	[Fact]
	public async Task Record_Duration_Histogram_On_DeleteSnapshotsOlderThanAsync_Success()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreDuration)
			.ToList();
		durations.ShouldNotBeEmpty();
		durations[^1].Value.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task Set_Version_Activity_Tag_On_DeleteSnapshotsOlderThanAsync()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 15L, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 15L, CancellationToken.None);

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.DeleteSnapshots);
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.Version).ShouldBe(15L);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe(AggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe(AggregateType);
		activity.GetTagItem(EventSourcingTags.Provider).ToString().ShouldBe(ProviderName);
	}

	// ------------------------------------------------------------------
	// DeleteSnapshotsOlderThanAsync — error path
	// ------------------------------------------------------------------

	[Fact]
	public async Task Record_Failure_Counter_On_DeleteSnapshotsOlderThanAsync_Error()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, A<CancellationToken>._))
			.Throws(new InvalidOperationException("delete older error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, CancellationToken.None).AsTask());

		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();
		ops.ShouldNotBeEmpty();
		ops[^1].Tags.ShouldContain(t => t.Key == EventSourcingTags.OperationResult && (string)t.Value! == EventSourcingTagValues.Failure);
	}

	[Fact]
	public async Task Set_Error_Activity_Status_On_DeleteSnapshotsOlderThanAsync_Error()
	{
		// Arrange
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, A<CancellationToken>._))
			.Throws(new InvalidOperationException("trace older error"));

		// Act
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 5L, CancellationToken.None).AsTask());

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.DeleteSnapshots);
		activity.ShouldNotBeNull();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
	}

	// ------------------------------------------------------------------
	// Provider tagging
	// ------------------------------------------------------------------

	[Fact]
	public async Task Tag_All_Metrics_With_Provider_Name()
	{
		// Arrange
		var snapshot = CreateFakeSnapshot();
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));
		A.CallTo(() => _innerFake.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);
		A.CallTo(() => _innerFake.DeleteSnapshotsAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);
		A.CallTo(() => _innerFake.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 1L, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		// Act
		await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);
		await _sut.DeleteSnapshotsAsync(AggregateId, AggregateType, CancellationToken.None);
		await _sut.DeleteSnapshotsOlderThanAsync(AggregateId, AggregateType, 1L, CancellationToken.None);

		// Assert — all counter recordings should have provider tag
		_meterListener.RecordObservableInstruments();
		foreach (var recording in _counterRecordings)
		{
			recording.Tags.ShouldContain(t => t.Key == EventSourcingTags.Provider && (string)t.Value! == ProviderName,
				$"Counter recording for '{recording.Name}' should have provider tag");
		}

		foreach (var recording in _histogramRecordings)
		{
			recording.Tags.ShouldContain(t => t.Key == EventSourcingTags.Provider && (string)t.Value! == ProviderName,
				$"Histogram recording for '{recording.Name}' should have provider tag");
		}
	}

	// ------------------------------------------------------------------
	// Activity tracing — common tags
	// ------------------------------------------------------------------

	[Fact]
	public async Task Set_AggregateId_And_Type_Activity_Tags_On_GetLatestSnapshot()
	{
		// Arrange
		A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, AggregateType, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));

		// Act
		await _sut.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None);

		// Assert
		var activity = _capturedActivities.FirstOrDefault(a => a.OperationName == EventSourcingActivities.GetSnapshot);
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe(AggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe(AggregateType);
		activity.GetTagItem(EventSourcingTags.Provider).ToString().ShouldBe(ProviderName);
	}

	// ------------------------------------------------------------------
	// TagCardinalityGuard overflow
	// ------------------------------------------------------------------

	[Fact]
	public async Task Guard_AggregateType_Tag_Cardinality()
	{
		// Arrange — the guard has max cardinality of 128
		// Generate 135 unique aggregate types to exceed the guard
		for (var i = 0; i < 135; i++)
		{
			var uniqueType = $"UniqueType_{i}";
			A.CallTo(() => _innerFake.GetLatestSnapshotAsync(AggregateId, uniqueType, A<CancellationToken>._))
				.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));

			await _sut.GetLatestSnapshotAsync(AggregateId, uniqueType, CancellationToken.None);
		}

		// Assert — last 7 should have "__other__" as aggregate type tag
		_meterListener.RecordObservableInstruments();
		var ops = _counterRecordings
			.Where(r => r.Name == EventSourcingMetricNames.SnapshotStoreOperations)
			.ToList();

		ops.Count.ShouldBe(135);

		var otherTags = ops
			.Where(r => r.Tags.Any(t => t.Key == EventSourcingTags.AggregateType && (string)t.Value! == "__other__"))
			.ToList();

		otherTags.ShouldNotBeEmpty();
	}

	// ------------------------------------------------------------------
	// Helpers
	// ------------------------------------------------------------------

	private static ISnapshot CreateFakeSnapshot(long version = 1L)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.AggregateId).Returns(AggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns(AggregateType);
		A.CallTo(() => snapshot.Version).Returns(version);
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-" + Guid.NewGuid().ToString("N")[..8]);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 0x01, 0x02, 0x03 });
		return snapshot;
	}
}
