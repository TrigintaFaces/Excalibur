using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

namespace Excalibur.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureSchedulerBackgroundServiceShould
{
	[Fact]
	public void Throw_for_null_scope_factory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureSchedulerBackgroundService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new ErasureSchedulerOptions()),
				NullLogger<ErasureSchedulerBackgroundService>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new ErasureSchedulerBackgroundService(
				scopeFactory,
				null!,
				NullLogger<ErasureSchedulerBackgroundService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new ErasureSchedulerBackgroundService(
				scopeFactory,
				Microsoft.Extensions.Options.Options.Create(new ErasureSchedulerOptions()),
				null!));
	}

	[Fact]
	public async Task Exit_immediately_when_disabled()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		var options = new ErasureSchedulerOptions { Enabled = false };
		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// No scope should be created when scheduler is disabled
		A.CallTo(() => scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	[Fact]
	public async Task Process_scheduled_erasures_when_enabled()
	{
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var queryObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_ = queryObserved.TrySetResult();
				return Task.FromResult<IReadOnlyList<ErasureStatus>>([]);
			});

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			queryObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Execute_successful_erasure_request()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var executionObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]),
				Task.FromResult<IReadOnlyList<ErasureStatus>>([]));

		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Invokes(() => executionObserved.TrySetResult(true))
			.Returns(ErasureExecutionResult.Succeeded(3, 42));

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			BatchSize = 10
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			executionObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Handle_failed_erasure_execution()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var failedStatusUpdated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.Invokes(() => failedStatusUpdated.TrySetResult(true))
			.Returns(ErasureWriteOutcome.Applied);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]),
				Task.FromResult<IReadOnlyList<ErasureStatus>>([]));

		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Returns(ErasureExecutionResult.Failed("Key not found"));

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxRetryAttempts = 0
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			failedStatusUpdated.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Handle_erasure_execution_exception()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var failedStatusUpdated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.Invokes(() => failedStatusUpdated.TrySetResult(true))
			.Returns(ErasureWriteOutcome.Applied);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]),
				Task.FromResult<IReadOnlyList<ErasureStatus>>([]));

		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("KMS unavailable"));

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxRetryAttempts = 0
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			failedStatusUpdated.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should mark as failed after exception
		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Continue_after_processing_error()
	{
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var secondQueryObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		var callCount = 0;
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				if (count == 1)
				{
					throw new InvalidOperationException("Database offline");
				}

				if (count >= 2)
				{
					_ = secondQueryObserved.TrySetResult();
				}

				return Task.FromResult<IReadOnlyList<ErasureStatus>>([]);
			});

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondQueryObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should have attempted more than once (continues after error)
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceOrMore();
	}

	[Fact]
	public async Task Handle_query_store_not_supported()
	{
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStoreLookupObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Invokes(() => { _ = queryStoreLookupObserved.TrySetResult(); })
			.Returns(null);

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		// The service catches exceptions in the processing loop, so it should still run
		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			queryStoreLookupObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should have tried to get the query store
		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Retry_failed_erasure_before_marking_permanently_failed()
	{
		// Regression test for Sprint 677 T.10 (j13lz): retry logic with exponential backoff.
		// With MaxRetryAttempts=2, the first failure should reschedule (Scheduled),
		// and the second failure should mark as Failed.
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var failedStatusUpdated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var retryStatusUpdated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		var scheduledRequest = CreateErasureStatus(requestId);

		// Return the same request repeatedly so the retry cycle can run
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]));

		// Always fail
		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Returns(ErasureExecutionResult.Failed("Simulated failure"));

		// Track Scheduled (retry) vs Failed (final) status updates
		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.Invokes(() => retryStatusUpdated.TrySetResult(true))
			.Returns(ErasureWriteOutcome.Applied);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.Invokes(() => failedStatusUpdated.TrySetResult(true))
			.Returns(ErasureWriteOutcome.Applied);

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureExecutor);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxRetryAttempts = 2,
			RetryDelayBase = TimeSpan.FromMilliseconds(10),
			UseExponentialBackoff = true
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);

		// First failure should reschedule
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			retryStatusUpdated.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));

		// After enough retries, should mark as permanently failed
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			failedStatusUpdated.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));

		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Verify: at least one Scheduled (retry) update happened before the final Failed
		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustHaveHappened();

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// When the store REFUSES the reschedule, the request is left alone: not marked Failed, and not
	/// rescheduled again.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The store refuses a write of <c>Scheduled</c> when the request is owned or terminal, and that
	/// refusal is the only authority on the question -- the read-then-check above it cannot see a claim
	/// that lands between its read and its write. A <see langword="false"/> return therefore means
	/// "another instance owns this now", and the one thing this instance must not do is write a terminal
	/// status over somebody else's running erasure.
	/// </para>
	/// <para>
	/// The liveness twin is <c>Retry_failed_erasure_before_marking_permanently_failed</c>, which stubs the
	/// same write to succeed and asserts the retry and the eventual Failed both happen. Without that
	/// twin this arm would be satisfied by a scheduler that never retried anything.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Leave_the_request_alone_when_the_store_refuses_the_reschedule()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var refused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var followingPolls = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var followingPollCount = 0;

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore))).Returns(queryStore);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (refused.Task.IsCompleted && Interlocked.Increment(ref followingPollCount) >= 3)
				{
					followingPolls.TrySetResult(true);
				}

				return Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]);
			});

		// The status read the guard performs still reports a resettable state, so the guard does NOT
		// short-circuit -- which is the whole point: this arm exercises the path where the guard allows
		// the write and the STORE is the one that refuses it. A fixture whose read reported InProgress
		// would stop at the guard and never reach the branch under test.
		A.CallTo(() => erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(scheduledRequest));

		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Returns(ErasureExecutionResult.Failed("Simulated failure"));

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.Invokes(() => refused.TrySetResult(true))
			// ClaimLost rather than StatusRefused, and the arm's own comment above says why: the store
			// refuses because ANOTHER INSTANCE OWNS the request. That used to be indistinguishable from
			// an illegal transition, which is the conflation the outcome type exists to end.
			.Returns(ErasureWriteOutcome.ClaimLost);

		var (scopeFactory, scope) = SetupScopeFactory(erasureStore, erasureExecutor);
		using var ownedScope = scope;

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxRetryAttempts = 2,
			RetryDelayBase = TimeSpan.FromMilliseconds(10),
			UseExponentialBackoff = true
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		// Query entry proves earlier sequential loop iterations completed, not the current callback tail.
		await ObserveFollowingPollsAndStopAsync(sut, cts, refused.Task, followingPolls.Task);
		Volatile.Read(ref followingPollCount).ShouldBeGreaterThanOrEqualTo(3);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A request deferred by a legal hold must neither be retried nor marked failed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A legal hold is the system working correctly, not an error.</b> The executor reports the deferral
	/// as an unsuccessful result because its result type cannot say "lawfully deferred", and the scheduler
	/// used to route that into the ordinary retry path: the deferral spent the request's fault budget on a
	/// state no retry can change, and at exhaustion the status was overwritten with a terminal
	/// "failed after N attempts".
	/// </para>
	/// <para>
	/// <b>That terminal record is indistinguishable from a genuine execution failure</b> -- a key provider
	/// outage, a broken store -- so a consumer handing an auditor a list of failed erasures could not tell
	/// which were deferred by law and which were broken. It also destroys the only durable trace of WHY,
	/// because the status that explained it is gone and the hold's basis and case reference survive only
	/// inside an error string.
	/// </para>
	/// <para>
	/// Its LIVENESS twin is <see cref="Handle_failed_erasure_execution"/>, which asserts that an ORDINARY
	/// failure still reaches the terminal failed status. Without it this exemption would be satisfied by a
	/// scheduler that stopped marking anything failed at all.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	[Fact]
	public async Task Neither_retry_nor_fail_a_request_a_legal_hold_has_deferred()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var followingPolls = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var followingPollCount = 0;

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore))).Returns(queryStore);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (observed.Task.IsCompleted && Interlocked.Increment(ref followingPollCount) >= 3)
				{
					followingPolls.TrySetResult(true);
				}

				return Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]);
			});

		// What the store holds AFTER the executor deferred it: the execution path writes this status
		// itself, from inside the claimed window, before returning its unsuccessful result.
		var deferred = scheduledRequest with { Status = ErasureRequestStatus.BlockedByLegalHold };
		A.CallTo(() => erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Invokes(() => observed.TrySetResult(true))
			.Returns(Task.FromResult<ErasureStatus?>(deferred));

		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Returns(ErasureExecutionResult.Failed("Erasure blocked by active legal hold"));

		var (scopeFactory, scope) = SetupScopeFactory(erasureStore, erasureExecutor);
		using var ownedScope = scope;

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxRetryAttempts = 2,
			RetryDelayBase = TimeSpan.FromMilliseconds(10),
			UseExponentialBackoff = true
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		// Query entry proves earlier sequential loop iterations completed, not the current callback tail.
		await ObserveFollowingPollsAndStopAsync(sut, cts, observed.Task, followingPolls.Task);
		Volatile.Read(ref followingPollCount).ShouldBeGreaterThanOrEqualTo(3);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// A REFUSAL MUST NOT SPEND THE FAULT BUDGET, and the fixture is chosen so that only the OUTCOME can
	// spare it. The failure path already exempts four statuses by reading the request back -- the deferred
	// one, and the owned-or-terminal set -- so a refusal whose request is any of those would pass this arm
	// with or without the outcome branch, and prove nothing.
	//
	// Here the request is still SCHEDULED and the executor REFUSED it. No status exemption fires. Without
	// the outcome branch the scheduler increments a per-process counter, reschedules, and at exhaustion
	// writes a TERMINAL Failed -- which its own query never returns and no pass enumerates, so a correct
	// withholding becomes a permanent non-erasure within MaxRetryAttempts poll intervals.
	//
	// Mutant: delete the `result.Outcome == ErasureExecutionOutcome.Refused` branch so a refusal falls into
	// the failure path -- this arm goes RED and every other arm in this file stays green.
	[Fact]
	public async Task Not_spend_the_fault_budget_on_a_refusal_the_executor_never_attempted()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureExecutor = A.Fake<IErasureExecutor>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var followingPolls = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var followingPollCount = 0;

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore))).Returns(queryStore);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (observed.Task.IsCompleted && Interlocked.Increment(ref followingPollCount) >= 3)
				{
					followingPolls.TrySetResult(true);
				}

				return Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]);
			});

		// STILL SCHEDULED after the refusal: the executor declined before taking the claim, so it changed
		// nothing. That is what makes every status-based exemption inapplicable here.
		A.CallTo(() => erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(scheduledRequest));

		// THE SIGNAL IS ON THE EXECUTOR, NOT ON THE STATUS READ, and that is a fixture correctness point
		// rather than a style choice. The status read happens only INSIDE the failure path -- which is
		// exactly the path this arm asserts is not taken -- so keying the signal on it makes the arm time
		// out on the FIXED code and pass only on the broken code. The first version of this arm did that.
		A.CallTo(() => erasureExecutor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Invokes(() => observed.TrySetResult(true))
			.Returns(ErasureExecutionResult.Refused("the deployment records a retention this instance does not hold"));

		var (scopeFactory, scope) = SetupScopeFactory(erasureStore, erasureExecutor);
		using var ownedScope = scope;

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxRetryAttempts = 2,
			RetryDelayBase = TimeSpan.FromMilliseconds(10),
			UseExponentialBackoff = true
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		// Query entry proves earlier sequential loop iterations completed, not the current callback tail.
		await ObserveFollowingPollsAndStopAsync(sut, cts, observed.Task, followingPolls.Task);
		Volatile.Read(ref followingPollCount).ShouldBeGreaterThanOrEqualTo(3);

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		A.CallTo(() => erasureStore.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled, A<ErasureRequestStatus?>._, A<string>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static async Task ObserveFollowingPollsAndStopAsync(
		ErasureSchedulerBackgroundService service, CancellationTokenSource cancellation, Task firstObservation, Task followingPolls)
	{
		Exception? primaryFailure = null;
		Exception? cleanupFailure = null;
		var timeout = global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10));
		try
		{
			await service.StartAsync(cancellation.Token).ConfigureAwait(false);
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(firstObservation, timeout);
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(followingPolls, timeout);
		}
		catch (Exception error)
		{
			primaryFailure = error;
			throw;
		}
		finally
		{
			try
			{
				await cancellation.CancelAsync().ConfigureAwait(false);
				await service.StopAsync(CancellationToken.None).WaitAsync(timeout).ConfigureAwait(false);
			}
			catch (Exception error)
			{
				cleanupFailure = error;
			}
			finally
			{
				try { service.Dispose(); }
				catch (Exception error) { cleanupFailure = cleanupFailure is null ? error : new AggregateException(cleanupFailure, error); }
			}

			if (primaryFailure is not null && cleanupFailure is not null)
			{
				primaryFailure.Data["ErasureWorkerCleanup"] = cleanupFailure;
			}
		}

		if (cleanupFailure is not null) { throw cleanupFailure; }
	}

	private static ErasureStatus CreateErasureStatus(Guid requestId) =>
		new()
		{
			TenantPartition = KeyedTenantPartition.Untenanted,
			RequestId = requestId,
			DataSubjectIdHash = "abc123hash",
			IdType = DataSubjectIdType.Email,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = ErasureRequestStatus.Scheduled,
			RequestedBy = "test@example.com",
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-3),
			ScheduledExecutionAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			UpdatedAt = DateTimeOffset.UtcNow
		};

	private static (IServiceScopeFactory, IServiceScope) SetupScopeFactory(
		IErasureStore erasureStore,
		IErasureExecutor erasureExecutor)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IErasureStore)))
			.Returns(erasureStore);
		A.CallTo(() => serviceProvider.GetService(typeof(IErasureExecutor)))
			.Returns(erasureExecutor);

		// CreateAsyncScope() is an extension method that calls CreateScope() internally
		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		return (scopeFactory, scope);
	}
}
