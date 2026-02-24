// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TelemetryLeaderElection"/>.
/// Validates metric recording, event forwarding, activity tracing, and TagCardinalityGuard overflow.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class TelemetryLeaderElectionShould : UnitTestBase
{
	private readonly ILeaderElection _innerFake;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetryLeaderElection _sut;
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterRecordings = [];
	private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _histogramRecordings = [];

	public TelemetryLeaderElectionShould()
	{
		_innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => _innerFake.CandidateId).Returns("node-1");
		A.CallTo(() => _innerFake.IsLeader).Returns(false);
		A.CallTo(() => _innerFake.CurrentLeaderId).Returns(null as string);

		_meter = new Meter(LeaderElectionTelemetryConstants.MeterName + ".Test." + Guid.NewGuid().ToString("N")[..8]);
		_activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName + ".Test");

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

		_sut = new TelemetryLeaderElection(_innerFake, _meter, _activitySource, "sqlserver");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_sut.DisposeAsync().AsTask().GetAwaiter().GetResult();
			_meterListener.Dispose();
			_meter.Dispose();
			_activitySource.Dispose();
		}

		base.Dispose(disposing);
	}

	// --- Constructor validation ---

	[Fact]
	public void Throw_When_Inner_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(null!, _meter, _activitySource, "sqlserver"));
	}

	[Fact]
	public void Throw_When_Meter_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(_innerFake, null!, _activitySource, "sqlserver"));
	}

	[Fact]
	public void Throw_When_ActivitySource_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(_innerFake, _meter, null!, "sqlserver"));
	}

	[Fact]
	public void Throw_When_ProviderName_Is_Null()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(_innerFake, _meter, _activitySource, null!));
	}

	// --- Property forwarding ---

	[Fact]
	public void Forward_CandidateId_From_Inner()
	{
		A.CallTo(() => _innerFake.CandidateId).Returns("test-candidate");
		_sut.CandidateId.ShouldBe("test-candidate");
	}

	[Fact]
	public void Forward_IsLeader_From_Inner()
	{
		A.CallTo(() => _innerFake.IsLeader).Returns(true);
		_sut.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public void Forward_CurrentLeaderId_From_Inner()
	{
		A.CallTo(() => _innerFake.CurrentLeaderId).Returns("leader-x");
		_sut.CurrentLeaderId.ShouldBe("leader-x");
	}

	// --- StartAsync / StopAsync delegation ---

	[Fact]
	public async Task Delegate_StartAsync_To_Inner()
	{
		// Act
		await _sut.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _innerFake.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Delegate_StopAsync_To_Inner()
	{
		// Act
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _innerFake.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	// --- Acquisition counter ---

	[Fact]
	public void Record_Acquisition_Counter_On_BecameLeader()
	{
		// Act — raise BecameLeader on the inner fake
		_innerFake.BecameLeader += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));

		// Assert
		_meterListener.RecordObservableInstruments();
		var acq = _counterRecordings.Where(r => r.Name == LeaderElectionTelemetryConstants.MetricNames.Acquisitions).ToList();
		acq.ShouldNotBeEmpty();
		acq[0].Value.ShouldBe(1);
		acq[0].Tags.ShouldContain(t => t.Key == LeaderElectionTelemetryConstants.Tags.Result && (string)t.Value! == "acquired");
		acq[0].Tags.ShouldContain(t => t.Key == LeaderElectionTelemetryConstants.Tags.Provider && (string)t.Value! == "sqlserver");
		acq[0].Tags.ShouldContain(t => t.Key == LeaderElectionTelemetryConstants.Tags.Instance && (string)t.Value! == "node-1");
	}

	[Fact]
	public void Record_Acquisition_Lost_Counter_On_LostLeadership()
	{
		// Act — raise LostLeadership
		_innerFake.LostLeadership += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));

		// Assert
		_meterListener.RecordObservableInstruments();
		var lost = _counterRecordings.Where(r =>
			r.Name == LeaderElectionTelemetryConstants.MetricNames.Acquisitions &&
			r.Tags.Any(t => t.Key == LeaderElectionTelemetryConstants.Tags.Result && (string)t.Value! == "lost")).ToList();
		lost.ShouldNotBeEmpty();
		lost[0].Value.ShouldBe(1);
	}

	// --- Lease duration histogram ---

	[Fact]
	public void Record_Lease_Duration_When_Lost_Leadership()
	{
		// Arrange — become leader to start stopwatch
		_innerFake.BecameLeader += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));

		// Wait a tiny bit so duration > 0
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act — lose leadership
		_innerFake.LostLeadership += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings.Where(r => r.Name == LeaderElectionTelemetryConstants.MetricNames.LeaseDuration).ToList();
		durations.ShouldNotBeEmpty();
		durations[0].Value.ShouldBeGreaterThan(0);
		durations[0].Tags.ShouldContain(t => t.Key == LeaderElectionTelemetryConstants.Tags.Instance);
		durations[0].Tags.ShouldContain(t => t.Key == LeaderElectionTelemetryConstants.Tags.Provider && (string)t.Value! == "sqlserver");
	}

	[Fact]
	public async Task Record_Lease_Duration_On_StopAsync_If_Leader()
	{
		// Arrange — become leader
		_innerFake.BecameLeader += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Act — stop while still leader
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings.Where(r => r.Name == LeaderElectionTelemetryConstants.MetricNames.LeaseDuration).ToList();
		durations.ShouldNotBeEmpty();
		durations[0].Value.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task Not_Record_Lease_Duration_On_StopAsync_If_Not_Leader()
	{
		// Act — stop without ever becoming leader
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		_meterListener.RecordObservableInstruments();
		var durations = _histogramRecordings.Where(r => r.Name == LeaderElectionTelemetryConstants.MetricNames.LeaseDuration).ToList();
		durations.ShouldBeEmpty();
	}

	// --- Event forwarding ---

	[Fact]
	public void Forward_BecameLeader_Event()
	{
		// Arrange
		LeaderElectionEventArgs? captured = null;
		_sut.BecameLeader += (_, args) => captured = args;

		// Act
		_innerFake.BecameLeader += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));

		// Assert
		captured.ShouldNotBeNull();
		captured.CandidateId.ShouldBe("node-1");
	}

	[Fact]
	public void Forward_LostLeadership_Event()
	{
		// Arrange
		LeaderElectionEventArgs? captured = null;
		_sut.LostLeadership += (_, args) => captured = args;

		// Act
		_innerFake.LostLeadership += Raise.With(new LeaderElectionEventArgs("node-1", "test-resource"));

		// Assert
		captured.ShouldNotBeNull();
		captured.CandidateId.ShouldBe("node-1");
	}

	[Fact]
	public void Forward_LeaderChanged_Event()
	{
		// Arrange
		LeaderChangedEventArgs? captured = null;
		_sut.LeaderChanged += (_, args) => captured = args;

		// Act
		_innerFake.LeaderChanged += Raise.With(new LeaderChangedEventArgs("old-leader", "new-leader", "test-resource"));

		// Assert
		captured.ShouldNotBeNull();
		captured.PreviousLeaderId.ShouldBe("old-leader");
		captured.NewLeaderId.ShouldBe("new-leader");
	}

	// --- TagCardinalityGuard overflow ---

	[Fact]
	public async Task Overflow_Instance_Tag_When_Cardinality_Exceeded()
	{
		// Arrange — create a TelemetryLeaderElection with small cardinality guard
		// The default guard is 100. Simulate by raising events with many different candidateIds.
		// We use 100+ unique candidates to exceed the guard.
		var recordings = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
		using var localMeter = new Meter("LE.CardinalityTest." + Guid.NewGuid().ToString("N")[..8]);
		using var localSource = new ActivitySource("LE.CardinalityTest");
		using var localListener = new MeterListener();
		localListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter == localMeter)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		localListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
		{
			recordings.Add((instrument.Name, value, tags.ToArray()));
		});
		localListener.Start();

		var inner = A.Fake<ILeaderElection>();
		A.CallTo(() => inner.CandidateId).Returns("candidate-0");
		A.CallTo(() => inner.IsLeader).Returns(false);

		var sut = new TelemetryLeaderElection(inner, localMeter, localSource, "inmemory");

		// Act — raise 105 events with unique candidate IDs
		for (var i = 0; i < 105; i++)
		{
			inner.BecameLeader += Raise.With(new LeaderElectionEventArgs($"candidate-{i}", "test-resource"));
		}

		// Assert — first 100 should have unique instance tags, rest should be "__other__"
		localListener.RecordObservableInstruments();
		var acquisitions = recordings
			.Where(r => r.Name == LeaderElectionTelemetryConstants.MetricNames.Acquisitions)
			.ToList();

		acquisitions.Count.ShouldBe(105);

		var otherTags = acquisitions
			.Where(r => r.Tags.Any(t => t.Key == LeaderElectionTelemetryConstants.Tags.Instance && (string)t.Value! == "__other__"))
			.ToList();

		otherTags.ShouldNotBeEmpty();

		await sut.DisposeAsync();
	}

	// --- Dispose ---

	[Fact]
	public async Task Dispose_Inner_If_AsyncDisposable()
	{
		// Arrange
		var disposableInner = A.Fake<ILeaderElection>(x => x.Implements<IAsyncDisposable>());
		A.CallTo(() => disposableInner.CandidateId).Returns("test");
		A.CallTo(() => disposableInner.IsLeader).Returns(false);
		using var localMeter = new Meter("LE.DisposeTest." + Guid.NewGuid().ToString("N")[..8]);
		using var localSource = new ActivitySource("LE.DisposeTest");
		var telemetry = new TelemetryLeaderElection(disposableInner, localMeter, localSource, "test");

		// Act
		await telemetry.DisposeAsync();

		// Assert
		A.CallTo(() => ((IAsyncDisposable)disposableInner).DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Dispose_Inner_If_Disposable()
	{
		// Arrange — ILeaderElection : IAsyncDisposable, so IAsyncDisposable path always takes priority
		var disposableInner = A.Fake<ILeaderElection>(x => x.Implements<IDisposable>());
		A.CallTo(() => disposableInner.CandidateId).Returns("test");
		A.CallTo(() => disposableInner.IsLeader).Returns(false);
		using var localMeter = new Meter("LE.DisposeTest2." + Guid.NewGuid().ToString("N")[..8]);
		using var localSource = new ActivitySource("LE.DisposeTest2");
		var telemetry = new TelemetryLeaderElection(disposableInner, localMeter, localSource, "test");

		// Act
		await telemetry.DisposeAsync();

		// Assert — DisposeAsync preferred over Dispose when both are available
		A.CallTo(() => disposableInner.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_Is_Idempotent()
	{
		// Act — dispose twice
		await _sut.DisposeAsync();
		await _sut.DisposeAsync();

		// Assert — no exception thrown
	}
}
