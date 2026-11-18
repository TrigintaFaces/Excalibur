// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Binds the disposal-time relinquish forwarding of <see cref="TelemetryLeaderElection"/>.
/// An election that holds a tenure when it is disposed relinquishes it and announces that from inside
/// its own disposal. Because this decorator is the instance the DI pipeline hands the consumer, that
/// announcement has to reach the decorator's OWN subscribers, exactly once -- or no consumer can observe
/// a disposal-time relinquish through any provider, including the ones that correctly raise one.
/// The ordering of the inner dispose against the handler detach in
/// <see cref="TelemetryLeaderElection.DisposeAsync"/> is what these arms hold in place.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class TelemetryLeaderElectionDisposalShould : UnitTestBase
{
	[Fact]
	public async Task ForwardAnInnerRelinquishAnnouncedDuringAsyncDisposal()
	{
		// Arrange
		var inner = new AsyncDisposableRelinquishingElection();
		using var meter = NewMeter();
		using var activitySource = NewActivitySource();
		var sut = new TelemetryLeaderElection(inner, meter, activitySource, "fake");

		var observed = 0;
		sut.LostLeadership += (_, _) => observed++;

		// Act
		await sut.DisposeAsync();

		// Assert -- ShouldBe(1) binds BOTH the forwarding (not 0) and exactly-once (not 2): if the
		// decorator ever grows a relinquish announcement of its own on top of forwarding the inner's,
		// this reddens.
		observed.ShouldBe(
			1,
			"a relinquish announced from inside the inner's DisposeAsync must reach the decorator's own subscribers exactly once");
		inner.DisposeAsyncCalls.ShouldBe(1);
	}

	[Fact]
	public async Task ForwardAnInnerLeaderChangeAnnouncedDuringAsyncDisposal()
	{
		// Arrange
		var inner = new AsyncDisposableRelinquishingElection();
		using var meter = NewMeter();
		using var activitySource = NewActivitySource();
		var sut = new TelemetryLeaderElection(inner, meter, activitySource, "fake");

		var observed = 0;
		sut.LeaderChanged += (_, _) => observed++;

		// Act
		await sut.DisposeAsync();

		// Assert
		observed.ShouldBe(
			1,
			"a leader change announced from inside the inner's DisposeAsync must reach the decorator's own subscribers exactly once");
	}

	[Fact]
	public void RecordThatTheInnerIsAlwaysAsyncDisposable()
	{
		// ILeaderElection extends IAsyncDisposable, so TelemetryLeaderElection.DisposeAsync awaits the
		// inner unconditionally and carries no synchronous fallback. There used to be an
		// `else if (_inner is IDisposable)` branch; it was dead by construction rather than merely
		// untaken -- an inner that is not IAsyncDisposable cannot be assigned to that field, and a
		// fixture written to reach the branch does not compile -- and it has since been removed.
		//
		// THIS ARM DOES NOT ENFORCE THAT PREMISE AND CANNOT FAIL WHILE IT HOLDS. The compiler enforces
		// it: drop IAsyncDisposable from ILeaderElection and the decorator's `await _inner.DisposeAsync()`
		// stops compiling, so this project would not build and this arm would never run. Measured --
		// the break is CS1061 at that await, not a red test.
		//
		// It is kept as the executable record of a design intent the compiler enforces silently, which
		// is what the decorator's own comment points at when it says a sibling arm asserts the same
		// assignability. Read it as documentation, never as protection.
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(ILeaderElection)).ShouldBeTrue(
			"ILeaderElection is expected to extend IAsyncDisposable; the decorator awaits the inner unconditionally on that basis, and the compiler is what holds it");
	}

	[Fact]
	public async Task NotAnnounceASecondRelinquishWhenDisposedTwice()
	{
		// Arrange
		var inner = new AsyncDisposableRelinquishingElection();
		using var meter = NewMeter();
		using var activitySource = NewActivitySource();
		var sut = new TelemetryLeaderElection(inner, meter, activitySource, "fake");

		var observed = 0;
		sut.LostLeadership += (_, _) => observed++;

		// Act
		await sut.DisposeAsync();
		await sut.DisposeAsync();

		// Assert -- the second disposal must be a no-op: no second announcement, no throw, and the
		// inner must not be disposed twice either.
		observed.ShouldBe(1, "disposal is idempotent: a second dispose must not announce a second relinquish");
		inner.DisposeAsyncCalls.ShouldBe(1, "the inner must be disposed once, not once per decorator dispose");
	}

	private static Meter NewMeter() =>
		new(LeaderElectionTelemetryConstants.MeterName + ".DisposalTest." + Guid.NewGuid().ToString("N")[..8]);

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Regression")]
	public async Task DetachAllHandlersEvenWhenInnerDisposalFails(bool canceled)
	{
		var inner = A.Fake<ILeaderElection>();
		var tail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => inner.NotificationsCompleted).Returns(tail.Task);
		A.CallTo(() => inner.CandidateId).Returns("node-1");
		Exception failure = canceled
			? new OperationCanceledException("inner disposal canceled")
			: new InvalidOperationException("inner disposal failed");
		A.CallTo(() => inner.DisposeAsync()).ReturnsLazily(() =>
		{
			// This fake has no asynchronous delivery workers; its terminal tail is now sealed.
			tail.TrySetResult();
			return ValueTask.FromException(failure);
		});
		using var meter = NewMeter();
		using var activitySource = NewActivitySource();
		var sut = new TelemetryLeaderElection(inner, meter, activitySource, "fake");
		var became = 0;
		var lost = 0;
		var changed = 0;
		var failed = 0;
		sut.BecameLeader += (_, _) => became++;
		sut.LostLeadership += (_, _) => lost++;
		sut.LeaderChanged += (_, _) => changed++;
		sut.AcquisitionFailed += (_, _) => failed++;

		void RaiseAllEvents()
		{
			inner.BecameLeader += Raise.With(new LeaderElectionEventArgs("node-1", "resource-1"));
			inner.LostLeadership += Raise.With(new LeaderElectionEventArgs("node-1", "resource-1"));
			inner.LeaderChanged += Raise.With(new LeaderChangedEventArgs("node-1", null, "resource-1"));
			inner.AcquisitionFailed += Raise.With(new LeaderElectionAcquisitionFailedEventArgs(
				"node-1", "resource-1", "unavailable", DateTimeOffset.UtcNow));
		}

		RaiseAllEvents();
		(became, lost, changed, failed).ShouldBe((1, 1, 1, 1));

		var thrown = await Record.ExceptionAsync(async () => await sut.DisposeAsync());
		thrown.ShouldBeSameAs(failure, "cleanup must preserve the inner disposal exception");
		await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
		RaiseAllEvents();
		(became, lost, changed, failed).ShouldBe((1, 1, 1, 1),
			"a failed disposal must still detach every event subscription");
		(await Record.ExceptionAsync(async () => await sut.DisposeAsync())).ShouldBeSameAs(failure,
			"every disposal must retain the first actual failure instead of reporting success");
		A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	private static ActivitySource NewActivitySource() =>
		new(LeaderElectionTelemetryConstants.ActivitySourceName + ".DisposalTest");

	/// <summary>
	/// Minimal hand-written <see cref="ILeaderElection"/>. A FakeItEasy fake cannot express the property
	/// under test -- the announcement has to be raised from INSIDE the inner's own disposal.
	/// </summary>
#pragma warning disable CS0067 // BecameLeader/AcquisitionFailed are interface members this fake never raises
	private sealed class AsyncDisposableRelinquishingElection : ILeaderElection
	{
		private readonly TaskCompletionSource _tail = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Task NotificationsCompleted => _tail.Task;
		public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

		public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

		public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

		public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

		public string CandidateId => "node-1";

		public bool IsLeader => false;

		public Leadership? CurrentLeadership => null;

		public string? CurrentLeaderId => null;

		public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public int DisposeAsyncCalls { get; private set; }

        private void InvokeAll<T>(EventHandler<T>? handlers, T args, ref Exception? failure) where T : EventArgs
        {
            if (handlers is null) { return; }
            foreach (EventHandler<T> handler in handlers.GetInvocationList())
            {
                try { handler(this, args); }
                catch (Exception error) { failure ??= error; }
            }
        }


		/// <summary>
		/// Relinquishes a held tenure and announces it from inside disposal, the way a real provider does.
		/// </summary>
		public ValueTask DisposeAsync()
		{
			DisposeAsyncCalls++;
			Exception? notificationFailure = null;
			InvokeAll(LostLeadership, new LeaderElectionEventArgs("node-1", "resource-1"), ref notificationFailure);
			InvokeAll(LeaderChanged, new LeaderChangedEventArgs("node-1", null, "resource-1"), ref notificationFailure);
			if (notificationFailure is null) { _tail.TrySetResult(); }
			else { _tail.TrySetException(notificationFailure); }
			return ValueTask.CompletedTask;
		}
	}
#pragma warning restore CS0067
}
