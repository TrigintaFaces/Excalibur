// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.InMemory;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Regression locks for the <c>AcquisitionFailed</c> event + telemetry <c>Result="failed"</c>
/// counter (S863 / fvqc9k).
/// </summary>
/// <remarks>
/// <para>
/// Covers spec acceptance criteria:
/// <list type="bullet">
/// <item><description>AC-f1: a candidate losing the acquisition race raises <c>AcquisitionFailed</c> with candidate id + reason.</description></item>
/// <item><description>AC-f4: an exception during acquisition raises <c>AcquisitionFailed</c> with the exception attached.</description></item>
/// <item><description>AC-f2: <see cref="TelemetryLeaderElection"/> increments the acquisitions counter with tag <c>Result="failed"</c> when the inner election raises <c>AcquisitionFailed</c>.</description></item>
/// <item><description>EC-f1: a subscriber throwing in the <c>AcquisitionFailed</c> handler must NOT break the acquire loop (guarded raise).</description></item>
/// </list>
/// </para>
/// <para>
/// Unit-covered providers: InMemory (lost-race path, guarded raise). The exception-during-acquire
/// path in the Consul/Redis/SqlServer/Postgres/MongoDB/Kubernetes providers requires real
/// infrastructure and is exercised by the integration suites; AC-f4's exception-plumbing is locked
/// here through the fakeable <see cref="ILeaderElection"/> seam + the telemetry decorator.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class LeaderElectionAcquisitionFailedShould : UnitTestBase
{
	// ---- AC-f1: losing the race raises AcquisitionFailed (InMemory provider) ----

	[Fact]
	public async Task Raise_AcquisitionFailed_With_CandidateId_And_Reason_When_Losing_The_Race()
	{
		// Arrange — two candidates contending for the SAME resource via shared state.
		var resource = $"race-resource-{Guid.NewGuid():N}";
		var sharedState = new InMemoryLeaderElectionSharedState();

		await using var winner = CreateElection(resource, "winner-1", sharedState);
		await using var loser = CreateElection(resource, "loser-2", sharedState);

		LeaderElectionAcquisitionFailedEventArgs? captured = null;
		loser.AcquisitionFailed += (_, args) => captured = args;

		// Act — winner acquires first, then loser attempts and loses the race.
		await winner.StartAsync(CancellationToken.None);
		await loser.StartAsync(CancellationToken.None);

		// Assert — the loser raised AcquisitionFailed identifying itself, with a lost-race reason and no exception.
		winner.IsLeader.ShouldBeTrue();
		loser.IsLeader.ShouldBeFalse();

		captured.ShouldNotBeNull();
		captured.CandidateId.ShouldBe("loser-2");
		captured.ResourceName.ShouldBe(resource);
		captured.Reason.ShouldNotBeNullOrWhiteSpace();
		captured.Reason.ShouldContain("race");
		captured.Exception.ShouldBeNull();
	}

	[Fact]
	public async Task Not_Raise_AcquisitionFailed_When_Candidate_Wins_The_Race()
	{
		// Arrange — single uncontended candidate.
		var resource = $"solo-resource-{Guid.NewGuid():N}";
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var winner = CreateElection(resource, "solo-1", sharedState);

		var raised = false;
		winner.AcquisitionFailed += (_, _) => raised = true;

		// Act
		await winner.StartAsync(CancellationToken.None);

		// Assert — winning does NOT raise AcquisitionFailed.
		winner.IsLeader.ShouldBeTrue();
		raised.ShouldBeFalse();
	}

	// ---- EC-f1: a throwing subscriber must not break the acquire loop ----

	[Fact]
	public async Task Not_Break_Acquire_Loop_When_AcquisitionFailed_Subscriber_Throws()
	{
		// Arrange — winner holds the lease; loser has a throwing AcquisitionFailed handler.
		var resource = $"guard-resource-{Guid.NewGuid():N}";
		var sharedState = new InMemoryLeaderElectionSharedState();

		await using var winner = CreateElection(resource, "winner-1", sharedState);
		await using var loser = CreateElection(resource, "loser-2", sharedState);

		await winner.StartAsync(CancellationToken.None);

		var handlerInvoked = false;
		loser.AcquisitionFailed += (_, _) =>
		{
			handlerInvoked = true;
			throw new InvalidOperationException("subscriber blew up");
		};

		// Act — the loser's StartAsync raises AcquisitionFailed into the throwing handler.
		// The guarded raise must swallow the exception so StartAsync completes normally.
		await Should.NotThrowAsync(async () => await loser.StartAsync(CancellationToken.None));

		// Assert — handler ran (raise happened), yet the loop survived and state is consistent.
		handlerInvoked.ShouldBeTrue();
		loser.IsLeader.ShouldBeFalse();
		winner.IsLeader.ShouldBeTrue();
	}

	// ---- AC-f2 + AC-f4: telemetry decorator counts Result="failed" and forwards the exception ----

	[Fact]
	public async Task Increment_Acquisitions_Counter_With_Result_Failed_When_Inner_Raises_AcquisitionFailed()
	{
		// Arrange
		var innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => innerFake.CandidateId).Returns("node-1");
		A.CallTo(() => innerFake.IsLeader).Returns(false);

		using var harness = new CounterHarness();
		await using var sut = new TelemetryLeaderElection(innerFake, harness.Meter, harness.ActivitySource, "inmemory");

		// Act — inner election reports a failed acquisition (lost race).
		innerFake.AcquisitionFailed += Raise.With(
			new LeaderElectionAcquisitionFailedEventArgs("node-1", "test-resource", "lost the acquisition race", TimeProvider.System.GetUtcNow()));

		// Assert — a single acquisitions counter measurement tagged Result="failed" was recorded.
		var failed = harness.CounterRecordings
			.Where(r => r.Name == LeaderElectionTelemetryConstants.MetricNames.Acquisitions &&
				r.Tags.Any(t => t.Key == LeaderElectionTelemetryConstants.TagNames.Result && (string)t.Value! == "failed"))
			.ToList();

		failed.ShouldNotBeEmpty();
		failed.ShouldContain(entry => entry.Value == 1);
		failed[^1].Tags.ShouldContain(t =>
			t.Key == LeaderElectionTelemetryConstants.TagNames.Provider && (string)t.Value! == "inmemory");
		failed[^1].Tags.ShouldContain(t =>
			t.Key == LeaderElectionTelemetryConstants.TagNames.Instance && (string)t.Value! == "node-1");
	}

	[Fact]
	public async Task Forward_AcquisitionFailed_With_Exception_Attached_When_Acquisition_Errors()
	{
		// Arrange
		var innerFake = A.Fake<ILeaderElection>();
		A.CallTo(() => innerFake.CandidateId).Returns("node-1");
		A.CallTo(() => innerFake.IsLeader).Returns(false);

		using var harness = new CounterHarness();
		await using var sut = new TelemetryLeaderElection(innerFake, harness.Meter, harness.ActivitySource, "inmemory");

		LeaderElectionAcquisitionFailedEventArgs? forwarded = null;
		sut.AcquisitionFailed += (_, args) => forwarded = args;

		var cause = new TimeoutException("acquire timed out");

		// Act — inner election reports an ERROR during acquisition with the exception attached.
		innerFake.AcquisitionFailed += Raise.With(
			new LeaderElectionAcquisitionFailedEventArgs("node-1", "test-resource", "error during acquisition", TimeProvider.System.GetUtcNow(), cause));

		// Assert — the decorator forwards the event carrying the same exception instance,
		// and still records the failed acquisition.
		forwarded.ShouldNotBeNull();
		forwarded.Exception.ShouldBeSameAs(cause);
		forwarded.Reason.ShouldContain("error");

		harness.CounterRecordings.ShouldContain(r =>
			r.Name == LeaderElectionTelemetryConstants.MetricNames.Acquisitions &&
			r.Tags.Any(t => t.Key == LeaderElectionTelemetryConstants.TagNames.Result && (string)t.Value! == "failed"));
	}

	private static InMemoryLeaderElection CreateElection(
		string resource,
		string instanceId,
		InMemoryLeaderElectionSharedState sharedState)
	{
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = instanceId,
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(30),
			StepDownWhenUnhealthy = false,
		});

		return new InMemoryLeaderElection(resource, options, logger: null, sharedState);
	}

	/// <summary>
	/// Captures long-valued counter measurements emitted by a private meter, mirroring the harness in
	/// <c>TelemetryLeaderElectionShould</c>.
	/// </summary>
	private sealed class CounterHarness : IDisposable
	{
		private readonly MeterListener _listener;

		public CounterHarness()
		{
			Meter = new Meter(LeaderElectionTelemetryConstants.MeterName + ".Test." + Guid.NewGuid().ToString("N")[..8]);
			ActivitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName + ".Test");

			_listener = new MeterListener
			{
				InstrumentPublished = (instrument, listener) =>
				{
					if (instrument.Meter == Meter)
					{
						listener.EnableMeasurementEvents(instrument);
					}
				},
			};
			_listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
			{
				CounterRecordings.Add((instrument.Name, value, tags.ToArray()));
			});
			_listener.Start();
		}

		public Meter Meter { get; }

		public ActivitySource ActivitySource { get; }

		public List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> CounterRecordings { get; } = [];

		public void Dispose()
		{
			_listener.Dispose();
			Meter.Dispose();
			ActivitySource.Dispose();
		}
	}
}
