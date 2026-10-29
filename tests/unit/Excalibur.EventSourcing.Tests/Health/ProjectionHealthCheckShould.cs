// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Health;

/// <summary>
/// The projection health check: how far behind the read models are, and whether applying has failed.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms drive the lag READ MODEL, not a field.</b> The earlier version of this suite assigned
/// <c>ProjectionHealthState.AsyncLag</c> directly and asserted the resulting status — three green arms
/// over a field that <b>no production host ever assigned</b>. The check therefore reported healthy at
/// every lag, including a host that had stopped entirely, and the suite could not see it because the
/// suite was the only thing writing the value.
/// </para>
/// <para>
/// That is the failure this file now exists to prevent, so the fake below is a lag <i>source</i> — the
/// seam the check really consults — and no arm writes a progress figure the production code would have
/// to publish for itself.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHealthCheckShould
{
	private const string Subscription = "AsyncProjectionProcessingHost";

	// A cursor is addressed by the pair, so an arm about one cursor still has to name its projection.
	// Lag is reported per subscription, so which projection it is does not matter here -- only that the
	// pair is the key.
	private const string ProjectionKey = "Proj.A";

	private readonly ProjectionHealthState _state = new();

	[Fact]
	public async Task ReportHealthyWhenNothingIsBehindAndNothingHasFailed()
	{
		var check = Create(lag: 0);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("operating normally");
	}

	[Fact]
	public async Task ReportUnhealthyWhenASubscriptionIsFurtherBehindThanTheThreshold()
	{
		// THE ARM THAT MATTERS, and the one the previous suite only appeared to cover. A host that has
		// stopped -- crashed, shut down with its process, an IIS app pool idled out, standing by for a
		// leadership it will never be granted -- emits no exception and no log line. Growing distance
		// between the head and the checkpoint is the only evidence that exists.
		var check = Create(lag: 1500, head: 1500, checkpoint: 0);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("1500");
		result.Description.ShouldContain(
			Subscription, customMessage: "the verdict must name WHICH subscription stopped advancing");
	}

	[Fact]
	public async Task ReportDegradedBetweenTheTwoThresholds()
	{
		var check = Create(lag: 500);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public async Task ReportDegradedWhenNoLagReadModelIsRegisteredAtAll()
	{
		// Registering projections without enabling projection processing leaves nothing applying them.
		// Healthy here is how that host looks identical to a fully caught-up one -- which is the defect
		// this whole area has been carrying, so the absence must be visible rather than benign.
		var check = new ProjectionHealthCheck(_state, Options.Create(new ProjectionHealthCheckOptions()), lag: null);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("EnableProjectionProcessing");
	}

	[Fact]
	public async Task ReportDegradedWhenLagCannotBeMeasured()
	{
		// A provider with no global-stream head gives nothing to subtract a checkpoint from. Unknown is
		// not the same as zero, and reporting healthy would assert a measurement nobody took.
		var check = new ProjectionHealthCheck(
			_state,
			Options.Create(new ProjectionHealthCheckOptions()),
			new StubLagReadModel(new ProjectionLagReport(ProjectionLagAvailability.NoHeadSource, [])));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("cannot be measured");
	}

	[Fact]
	public async Task ReportDegradedAfterAnApplyError()
	{
		_state.RecordInlineError("OrderSummary");

		var check = Create(lag: 0, errorWindow: TimeSpan.FromMinutes(5));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("OrderSummary");
	}

	[Fact]
	public async Task ReportHealthyOnceAnApplyErrorHasAgedOutOfItsWindow()
	{
		_state.RecordInlineError("OrderSummary");

		var check = Create(lag: 0, errorWindow: TimeSpan.Zero);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task PreferTheWorstStateWhenAStalledSubscriptionAlsoHasARecentError()
	{
		// A recent error used to return Degraded before lag was even looked at, so a read model a hundred
		// thousand events behind reported the smaller problem. The error is a visible fault the host
		// retries; unbounded lag means nothing is advancing at all.
		_state.RecordInlineError("OrderSummary");

		var check = Create(lag: 1500, errorWindow: TimeSpan.FromMinutes(5));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task JudgeByTheFurthestBehindSubscriptionRatherThanTheAverage()
	{
		// One stalled projection is a fault even when every other subscription is current. An average
		// over four healthy streams and one dead one reports healthy.
		var check = new ProjectionHealthCheck(
			_state,
			Options.Create(new ProjectionHealthCheckOptions
			{
				UnhealthyLagThreshold = 1000,
				DegradedLagThreshold = 100,
			}),
			new StubLagReadModel(new ProjectionLagReport(
				ProjectionLagAvailability.Measured,
				[
					new ProjectionLag("current-a", 900, 900, 0),
					new ProjectionLag("current-b", 900, 900, 0),
					new ProjectionLag("stalled", 0, 5000, 5000),
					new ProjectionLag("current-c", 900, 900, 0),
				])));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("stalled");
	}

	[Fact]
	public async Task ReportUnhealthyWhenHaltedEvenWithNoLagAndNoRecentError()
	{
		// THE HOLE THE LAG FIX LEFT OPEN. A host halted on a poison event does not advance, so on a quiet
		// stream -- a dev box, a low-traffic subscription -- it sits about one event from the head. Lag is
		// below every threshold, the apply error ages out of its window after five minutes, and the host
		// is frozen forever. Both of the other signals report healthy here; only a latch does not.
		_state.RecordHalt(Subscription);

		var check = Create(lag: 1, errorWindow: TimeSpan.Zero);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("stopped advancing");
		result.Description.ShouldContain(Subscription);
	}

	[Fact]
	public async Task ReportHealthyOnceTheHaltedSubscriptionAppliesCleanlyAgain()
	{
		// LIVENESS, and the arm that stops the latch becoming a one-way trip. A latch that can never be
		// cleared satisfies the arm above perfectly and poisons the health endpoint for the process
		// lifetime, so the clearing path has to be asserted too -- and it must be PROGRESS that clears
		// it, which is what RecordApplied represents.
		_state.RecordHalt(Subscription);
		_state.RecordApplied(Subscription, 42);

		var check = Create(lag: 0);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task NotClearAHaltBecauseADIFFERENTSubscriptionMadeProgress()
	{
		// Progress on one subscription says nothing about another. Clearing on any apply at all would let
		// a busy subscription mask a frozen one -- the furthest-behind problem again, in the latch.
		_state.RecordHalt(Subscription);
		_state.RecordApplied("some-other-subscription", 42);

		var check = Create(lag: 0);

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var opts = Options.Create(new ProjectionHealthCheckOptions());

		_ = Should.Throw<ArgumentNullException>(() => new ProjectionHealthCheck(null!, opts));
		_ = Should.Throw<ArgumentNullException>(() => new ProjectionHealthCheck(_state, null!));
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task ReportHealthyAfterACleanFirstBatchBeforeItsCheckpointIsWritten()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var head = A.Fake<IGlobalStreamQuery>();
		A.CallTo(() => head.GetHeadPositionAsync(A<CancellationToken>._)).Returns(10L);
		_state.RecordApplied(Subscription, 10);
		var check = new ProjectionHealthCheck(_state, Options.Create(new ProjectionHealthCheckOptions()),
			new ProjectionLagReadModel(checkpoints, head, expectedSubscription: Subscription));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Healthy);
		(await checkpoints.GetCheckpointAsync(
			Subscription, ProjectionKey, TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotTreatAnotherSubscriptionsApplyAsEvidenceTheExpectedOneStarted()
	{
		_state.RecordApplied("other", 10);
		var check = new ProjectionHealthCheck(_state, Options.Create(new ProjectionHealthCheckOptions()),
			new StubLagReadModel(new ProjectionLagReport(ProjectionLagAvailability.Measured,
				[new ProjectionLag(Subscription, null, 10, 10)])));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("no progress at all");
	}

	[Theory]
	[InlineData(10, 2000)]
	[InlineData(2000, 10)]
	[Trait("Pattern", "Regression")]
	public async Task UseTheGreaterOfDurableAndLocalProgress(long checkpoint, long applied)
	{
		_state.RecordApplied(Subscription, applied);
		var check = new ProjectionHealthCheck(_state,
			Options.Create(new ProjectionHealthCheckOptions { UnhealthyLagThreshold = 1000 }),
			new StubLagReadModel(new ProjectionLagReport(ProjectionLagAvailability.Measured,
				[new ProjectionLag(Subscription, checkpoint, 2000, 2000 - checkpoint)])));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task ReportUnhealthyWhenTheHeadOutrunsAStaleAppliedMark()
	{
		_state.RecordApplied(Subscription, 10);
		var check = new ProjectionHealthCheck(_state,
			Options.Create(new ProjectionHealthCheckOptions { UnhealthyLagThreshold = 1000 }),
			new StubLagReadModel(new ProjectionLagReport(ProjectionLagAvailability.Measured,
				[new ProjectionLag(Subscription, null, 2000, 2000)])));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("1990");
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotLetAShortNeverStartedSubscriptionMaskAnUnhealthyOne()
	{
		var check = new ProjectionHealthCheck(_state,
			Options.Create(new ProjectionHealthCheckOptions { UnhealthyLagThreshold = 1000 }),
			new StubLagReadModel(new ProjectionLagReport(ProjectionLagAvailability.Measured,
				[
					new ProjectionLag("never-started", null, 10, 10),
					new ProjectionLag("stalled", 0, 2000, 2000),
				])));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("stalled");
	}

	private ProjectionHealthCheck Create(long lag, long? head = null, long? checkpoint = null, TimeSpan? errorWindow = null)
	{
		var headPosition = head ?? lag;
		var checkpointPosition = checkpoint ?? (headPosition - lag);

		var options = new ProjectionHealthCheckOptions
		{
			UnhealthyLagThreshold = 1000,
			DegradedLagThreshold = 100,
		};

		if (errorWindow is { } window)
		{
			options.InlineErrorWindow = window;
		}

		return new ProjectionHealthCheck(
			_state,
			Options.Create(options),
			new StubLagReadModel(new ProjectionLagReport(
				ProjectionLagAvailability.Measured,
				[new ProjectionLag(Subscription, checkpointPosition, headPosition, lag)])));
	}

	private sealed class StubLagReadModel(ProjectionLagReport report) : IProjectionLagReadModel
	{
		public ValueTask<ProjectionLagReport> GetLagAsync(CancellationToken cancellationToken) =>
			new(report);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotReportHealthyForAConfiguredSubscriptionThatHasNeverRun()
	{
		// THE AUDIT'S CASE, and it composes the REAL read model against an empty checkpoint store
		// rather than handing the check an already-measured lag. That substitution is why the arms in
		// this file could not see it: the defect is in what the read model FINDS, not in how the check
		// interprets what it is given.
		//
		// Enumeration returns only subscriptions that have recorded progress, so a configured
		// projection with a 100,000-event backlog and no first run contributed no entry at all -- and
		// an empty entry list is exactly what "nothing is behind" looks like.
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var head = A.Fake<IGlobalStreamQuery>();
		A.CallTo(() => head.GetHeadPositionAsync(A<CancellationToken>._)).Returns(100_000L);

		var check = new ProjectionHealthCheck(
			_state,
			Options.Create(new ProjectionHealthCheckOptions()),
			new ProjectionLagReadModel(checkpoints, head, expectedSubscription: Subscription));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldNotBe(
			HealthStatus.Healthy,
			customMessage: "a subscription holding no checkpoint has applied nothing; healthy asserts the opposite");
		result.Description.ShouldContain("no progress at all");
		result.Description.ShouldContain(Subscription);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotReportHealthyForANeverStartedSubscriptionEvenOnAShortStream()
	{
		// The same condition BELOW every threshold. A never-started subscription on a stream of ten
		// events trails by ten, which is under the degraded threshold of 100 -- so a lag number alone
		// still reports healthy. Whether it is running at all is a different question from how far it
		// trails, which is why the check tests the missing checkpoint directly.
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var head = A.Fake<IGlobalStreamQuery>();
		A.CallTo(() => head.GetHeadPositionAsync(A<CancellationToken>._)).Returns(10L);

		var check = new ProjectionHealthCheck(
			_state,
			Options.Create(new ProjectionHealthCheckOptions { DegradedLagThreshold = 100, UnhealthyLagThreshold = 1000 }),
			new ProjectionLagReadModel(checkpoints, head, expectedSubscription: Subscription));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldNotBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task ReportHealthyOnceTheSubscriptionHasRecordedProgressAndIsCaughtUp()
	{
		// LIVENESS, and the arm that stops the two above being satisfied by a check that never reports
		// healthy. A subscription that HAS a checkpoint at the head is the good case and must pass.
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		_ = await checkpoints.AdvanceBoundCheckpointAsync(
			Subscription, ProjectionKey, null, 10, TestContext.Current.CancellationToken).ConfigureAwait(false);

		var head = A.Fake<IGlobalStreamQuery>();
		A.CallTo(() => head.GetHeadPositionAsync(A<CancellationToken>._)).Returns(10L);

		var check = new ProjectionHealthCheck(
			_state,
			Options.Create(new ProjectionHealthCheckOptions()),
			new ProjectionLagReadModel(checkpoints, head, expectedSubscription: Subscription));

		var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}
}
