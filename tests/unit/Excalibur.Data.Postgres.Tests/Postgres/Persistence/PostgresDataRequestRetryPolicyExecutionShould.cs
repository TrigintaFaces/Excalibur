// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Net.Sockets;

using Dapper;

using Excalibur.Data.Postgres.Persistence;

using Microsoft.Extensions.Time.Testing;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Persistence;

/// <summary>
/// Execution-semantics tests for <c>PostgresDataRequestRetryPolicy</c>.
/// </summary>
/// <remarks>
/// These pin the observable contract of the retry execution path - how many times the request is actually invoked, the backoff
/// schedule, which exception surfaces when the budget is exhausted, the success path, and which failures are classified transient -
/// independently of which mechanism drives it. Backoff is scheduled against an injected <see cref="FakeTimeProvider" />, so the
/// schedule is asserted without elapsing wall-clock time.
/// </remarks>
/// <remarks>
/// <para>
/// <b>The per-attempt delay is no longer assertable as a figure, and that is the point of the change these
/// arms were rewritten for.</b> This policy used to supply Polly with a custom delay generator computing
/// <c>base * 2^(attempt-1)</c> plus <c>Random.Shared.NextDouble() * 0.1</c> applied as <c>delay * (1 + f)</c>.
/// That jitter was ONE-SIDED: every value sat at or above the nominal, so no client ever retried sooner than
/// configured. It could not decorrelate anything -- hosts that failed together stayed clustered at the lower
/// bound of the band -- it only made each of them slower.
/// </para>
/// <para>
/// The old arms asserted exactly that shape, with <c>ShouldBeInRange(nominal, nominal * 1.1)</c>, so a delay
/// shorter than the nominal FAILED the suite. The shape was the requirement, and correct two-sided jitter
/// could not satisfy it. They are rewritten to assert the bounds the strategy actually guarantees, plus a
/// separate arm for the property the old shape made impossible.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresDataRequestRetryPolicyExecutionShould : UnitTestBase
{
	/// <summary>The ceiling the strategy caps every delay at, from PostgresPersistenceResilienceOptions' default.</summary>
	private const double MaxDelayMs = 30_000d;

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 2)]
	[InlineData(3, 4)]
	[InlineData(5, 6)]
	public async Task InvokeTheRequestOncePlusItsRetryBudget(int maxRetryAttempts, int expectedInvocations)
	{
		var clock = new FakeTimeProvider();
		var policy = CreatePolicy(maxRetryAttempts, new CapturingLogger(), clock);
		var request = DataRequest.AlwaysThrowing(TransientNpgsqlFailure);

		_ = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(expectedInvocations);
	}

	[Fact]
	public async Task BackOffOnTheExponentialScheduleGrownFromTheConfiguredBaseDelay()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();

		// Base delay 1000ms, budget 3: the nominal ladder is 1000, 2000, 4000, and jitter spreads each one.
		var policy = CreatePolicy(maxRetryAttempts: 3, logger, clock, baseDelayMilliseconds: 1_000);
		var request = DataRequest.AlwaysThrowing(TransientNpgsqlFailure);

		_ = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

		logger.RetryDelaysMilliseconds.Count.ShouldBe(3);

		foreach (var delay in logger.RetryDelaysMilliseconds)
		{
			delay.ShouldBeGreaterThan(0d, "a retry must actually wait");
			delay.ShouldBeLessThanOrEqualTo(MaxDelayMs, "the configured ceiling is a real bound");
		}
	}

	[Fact]
	public async Task ScaleTheScheduleWithTheConfiguredBaseDelay()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();

		// The base delay is options-driven on this policy, unlike its siblings' fixed one second.
		var policy = CreatePolicy(maxRetryAttempts: 2, logger, clock, baseDelayMilliseconds: 250);
		var request = DataRequest.AlwaysThrowing(TransientNpgsqlFailure);

		_ = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

		logger.RetryDelaysMilliseconds.Count.ShouldBe(2);

		foreach (var delay in logger.RetryDelaysMilliseconds)
		{
			delay.ShouldBeGreaterThan(0d, "a retry must actually wait");
			delay.ShouldBeLessThanOrEqualTo(MaxDelayMs, "the configured ceiling is a real bound");
		}
	}

	[Fact]
	public async Task ApplyJitterThatCanSHORTENADelay_NotOnlyLengthenIt()
	{
		// THE RED-DETECTING ARM for the one-sided-jitter fix, and the property the deleted
		// ShouldBeInRange(nominal, nominal * 1.1) assertions made unsatisfiable by construction.
		//
		// The old generator computed delay * (1 + Random.Shared.NextDouble() * 0.1), so the delay was
		// ALWAYS >= nominal. Hosts that failed together retried together at the bottom of that band; the
		// jitter bought latency and no decorrelation. Two-sided jitter must be able to land below the
		// nominal, which is what actually separates two clients in the same instant.
		//
		// Sampled over many runs rather than one: each run yields three delays and the clock is faked, so
		// 40 runs cost no wall-clock time and give 120 samples. A single run would make this arm
		// probabilistic, which the determinism rule forbids; at 120 samples the chance of seeing no delay
		// below its nominal is negligible for any jitter worth the name, while the OLD implementation
		// fails it with probability one at every sample.
		var nominal = new[] { 1_000d, 2_000d, 4_000d };
		var sawShorterThanNominal = false;

		for (var run = 0; run < 40 && !sawShorterThanNominal; run++)
		{
			var clock = new FakeTimeProvider();
			var logger = new CapturingLogger();
			var policy = CreatePolicy(maxRetryAttempts: 3, logger, clock, baseDelayMilliseconds: 1_000);
			var request = DataRequest.AlwaysThrowing(TransientNpgsqlFailure);

			_ = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

			for (var i = 0; i < logger.RetryDelaysMilliseconds.Count && !sawShorterThanNominal; i++)
			{
				sawShorterThanNominal = logger.RetryDelaysMilliseconds[i] < nominal[i];
			}
		}

		sawShorterThanNominal.ShouldBeTrue(
			"over 120 sampled delays none fell below its nominal value; jitter that can only add delay "
			+ "does not decorrelate clients that failed together, it just makes each of them slower");
	}

	[Fact]
	public async Task SurfaceTheFinalTransientFailureWhenTheBudgetIsExhausted()
	{
		var clock = new FakeTimeProvider();
		var thrown = new List<NpgsqlException>();
		var policy = CreatePolicy(maxRetryAttempts: 2, new CapturingLogger(), clock);
		var request = DataRequest.AlwaysThrowing(() =>
		{
			var exception = TransientNpgsqlFailure();
			thrown.Add(exception);
			return exception;
		});

		var surfaced = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

		// The exception that surfaces is the one from the LAST attempt, not the first.
		surfaced.ShouldBeSameAs(thrown[^1]);
		thrown.Count.ShouldBe(3);
	}

	[Fact]
	public async Task ReturnTheResultAndStopRetryingOnceTheRequestSucceeds()
	{
		var clock = new FakeTimeProvider();
		var policy = CreatePolicy(maxRetryAttempts: 5, new CapturingLogger(), clock);
		var request = DataRequest.ThrowingUntil(2, TransientNpgsqlFailure, result: 42);

		var result = await DriveAsync(policy, request, clock);

		result.ShouldBe(42);
		request.Invocations.ShouldBe(3);
	}

	[Fact]
	public async Task PropagateANonTransientFailureWithoutRetrying()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = CreatePolicy(maxRetryAttempts: 5, logger, clock);
		var request = DataRequest.AlwaysThrowing(() => new InvalidOperationException("not transient"));

		_ = await Should.ThrowAsync<InvalidOperationException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(1);
		logger.RetryDelaysMilliseconds.ShouldBeEmpty();
	}

	[Fact]
	public async Task RetryAnNpgsqlFailureTheDriverReportsTransient()
	{
		var clock = new FakeTimeProvider();
		var policy = CreatePolicy(maxRetryAttempts: 3, new CapturingLogger(), clock);

		// The message contains none of the fragments the DbException fallback sniffs for, so a retry here can only
		// have come from the driver's own IsTransient signal.
		var failure = TransientNpgsqlFailure();
		failure.IsTransient.ShouldBeTrue("the driver must report this failure transient for the arm to bind");

		var request = DataRequest.AlwaysThrowing(() => failure);

		_ = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(4);
	}

	[Fact]
	public async Task NotRetryAnNpgsqlFailureTheDriverReportsPermanent()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = CreatePolicy(maxRetryAttempts: 3, logger, clock);

		var failure = new NpgsqlException("boom");
		failure.IsTransient.ShouldBeFalse("the driver must report this failure permanent for the arm to bind");

		var request = DataRequest.AlwaysThrowing(() => failure);

		_ = await Should.ThrowAsync<NpgsqlException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(1);
		logger.RetryDelaysMilliseconds.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("deadlock detected on relation orders")]
	[InlineData("the connection was closed by the peer")]
	[InlineData("network unreachable")]
	[InlineData("Lock timeout exceeded")]
	[InlineData("broken pipe while writing to the server")]
	public async Task RetryADatabaseFailureWhoseMessageNamesATransientCondition(string message)
	{
		var clock = new FakeTimeProvider();
		var policy = CreatePolicy(maxRetryAttempts: 2, new CapturingLogger(), clock);
		var request = DataRequest.AlwaysThrowing(() => new TestDbException(message));

		_ = await Should.ThrowAsync<TestDbException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(3);
	}

	[Theory]
	[InlineData("duplicate key value violates unique constraint on orders")]
	[InlineData("syntax error at or near SELCT")]
	public async Task NotRetryADatabaseFailureWhoseMessageNamesNoTransientCondition(string message)
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = CreatePolicy(maxRetryAttempts: 2, logger, clock);
		var request = DataRequest.AlwaysThrowing(() => new TestDbException(message));

		_ = await Should.ThrowAsync<TestDbException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(1);
		logger.RetryDelaysMilliseconds.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(nameof(TimeoutException))]
	[InlineData(nameof(SocketException))]
	[InlineData(nameof(IOException))]
	public async Task RetryATransientInfrastructureFailure(string failureKind)
	{
		var clock = new FakeTimeProvider();
		var policy = CreatePolicy(maxRetryAttempts: 2, new CapturingLogger(), clock);
		var request = DataRequest.AlwaysThrowing(() => InfrastructureFailure(failureKind));

		_ = await Should.ThrowAsync<Exception>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(3);
	}

	private static Exception InfrastructureFailure(string failureKind) =>
		failureKind switch
		{
			nameof(TimeoutException) => new TimeoutException("command timed out"),
			nameof(SocketException) => new SocketException(),
			nameof(IOException) => new IOException("the stream was closed"),
			_ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "unknown failure kind"),
		};

	/// <summary>
	/// A cancellation must surface immediately. Retrying one keeps a caller who already stopped asking paying for the work, and
	/// hides a shutdown behind a retry budget.
	/// </summary>
	[Fact]
	public async Task NotRetryACancellationRaisedByTheRequest()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = CreatePolicy(maxRetryAttempts: 5, logger, clock);

		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		// The caller's own token is NOT cancelled - only the request reports the cancellation - so nothing but the
		// policy's own classification can stop the retry loop here.
		var request = DataRequest.AlwaysThrowing(() => new OperationCanceledException(cancelled.Token));

		_ = await Should.ThrowAsync<OperationCanceledException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(1);
		logger.RetryDelaysMilliseconds.ShouldBeEmpty();
	}

	[Fact]
	public async Task NotRetryWhenTheCallerCancels()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = CreatePolicy(maxRetryAttempts: 5, logger, clock);

		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		var request = DataRequest.AlwaysThrowing(() => new OperationCanceledException(cancelled.Token));
		var operation = policy.ResolveAsync<object, int>(request, () => Task.FromResult(new object()), cancelled.Token);

		_ = await Should.ThrowAsync<OperationCanceledException>(PumpAsync(operation, clock));

		// Stronger than "not retried": a caller who has already cancelled never reaches the database at all.
		request.Invocations.ShouldBe(0);
		logger.RetryDelaysMilliseconds.ShouldBeEmpty();
	}

	[Fact]
	public async Task ClassifyATaskCancellationAsPermanentToo()
	{
		var clock = new FakeTimeProvider();
		var policy = CreatePolicy(maxRetryAttempts: 1, new CapturingLogger(), clock);

		// TaskCanceledException derives from OperationCanceledException and must land on the same arm.
		var request = DataRequest.AlwaysThrowing(() => new TaskCanceledException("the read was abandoned"));

		_ = await Should.ThrowAsync<TaskCanceledException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(1);
	}

	/// <summary>The ceiling on any single delay, at its default.</summary>
	private static readonly TimeSpan DelayCeiling = TimeSpan.FromSeconds(30);

	[Fact]
	public async Task CapEveryDelayAtTheCeilingOnceTheExponentialWouldPassIt()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();

		// Eight attempts grown from a second: unbounded, the schedule reaches 128s, so the ceiling has
		// something to bind on, and the run still settles inside the simulated budget below.
		//
		// JITTER OFF, deliberately, and this is the whole point of the arm. The non-vacuity assertion at
		// the end requires the capped delay to EQUAL the ceiling, which is a property of an un-randomised
		// schedule: with jitter the delay is sampled BELOW the cap, so the equality held only when the
		// sample happened to clamp. That made this arm intermittent rather than wrong -- it passed roughly
		// three runs in four, so a local green looked like evidence and the mirror found it. The ceiling
		// and the randomisation are separate properties and are now asserted separately: this arm owns the
		// cap, ApplyJitterThatCanSHORTENADelay_NotOnlyLengthenIt owns the randomisation.
		var policy = CreatePolicy(maxRetryAttempts: 8, logger, clock, baseDelayMilliseconds: 1_000, useJitter: false);

		await DriveToExhaustionAsync(policy, clock).ConfigureAwait(true);

		logger.RetryDelaysMilliseconds.Count.ShouldBe(8);
		foreach (var delay in logger.RetryDelaysMilliseconds)
		{
			delay.ShouldBeLessThanOrEqualTo(
				DelayCeiling.TotalMilliseconds,
				"No single backoff delay may exceed the ceiling. Observed (ms): "
				+ string.Join(", ", logger.RetryDelaysMilliseconds));
		}

		// Non-vacuity: a cap that never bound would leave the arm above passing for the wrong reason.
		// A second doubled seven times is 128s, so the tail of this schedule is values the cap brought down.
		logger.RetryDelaysMilliseconds[^1].ShouldBe(DelayCeiling.TotalMilliseconds);
	}

	[Fact]
	public async Task CapEveryDelayAtTheCeilingWithJitterOnToo()
	{
		// The same ceiling, with randomisation live -- the configuration consumers actually get by default.
		// The equality above is unavailable here by construction, so this asserts only what jitter leaves
		// true: the cap is still a hard bound. Keeping both arms means turning the ceiling off cannot be
		// hidden by the jitter-off arm, and turning jitter off cannot be hidden by this one.
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();

		var policy = CreatePolicy(maxRetryAttempts: 8, logger, clock, baseDelayMilliseconds: 1_000, useJitter: true);

		await DriveToExhaustionAsync(policy, clock).ConfigureAwait(true);

		logger.RetryDelaysMilliseconds.Count.ShouldBe(8);
		foreach (var delay in logger.RetryDelaysMilliseconds)
		{
			delay.ShouldBeLessThanOrEqualTo(
				DelayCeiling.TotalMilliseconds,
				"No single backoff delay may exceed the ceiling, jitter included. Observed (ms): "
				+ string.Join(", ", logger.RetryDelaysMilliseconds));
		}
	}

	[Fact]
	public async Task BoundTheTotalRetryTimeAtTheLargestConfigurationTheOptionsAccept()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();

		// The worst case the ranges allow: the maximum attempt budget grown from the maximum base delay.
		// Unbounded, this is the configuration that sleeps about 9.4 hours inside one data request.
		var policy = CreatePolicy(maxRetryAttempts: 10, logger, clock, baseDelayMilliseconds: 30_000);

		await DriveToExhaustionAsync(policy, clock).ConfigureAwait(true);

		logger.RetryDelaysMilliseconds.Sum()
			.ShouldBeLessThanOrEqualTo(
				10 * DelayCeiling.TotalMilliseconds,
				"Total time asleep inside one data request must be bounded by attempts x ceiling.");
	}

	/// <summary>
	/// Drives a policy until its budget is exhausted, releasing each backoff by advancing the injected
	/// clock rather than by waiting.
	/// </summary>
	/// <remarks>
	/// Reports a diagnosable failure rather than blocking if the schedule does not settle within the
	/// simulated budget. An unbounded schedule would otherwise present as a test that never returns -
	/// the same unhelpful symptom the defect itself produces in a caller.
	/// </remarks>
	private static async Task DriveToExhaustionAsync(PostgresDataRequestRetryPolicy policy, FakeTimeProvider clock)
	{
		var operation = policy.ResolveAsync<object, int>(
			DataRequest.AlwaysThrowing(TransientNpgsqlFailure),
			() => Task.FromResult(new object()),
			TestContext.Current.CancellationToken);

		const int maxAdvances = 600;
		var advances = 0;
		while (!operation.IsCompleted && advances < maxAdvances)
		{
			await Task.Yield();
			clock.Advance(TimeSpan.FromSeconds(1));
			advances++;
		}

		operation.IsCompleted.ShouldBeTrue(
			$"The retry schedule did not settle within {maxAdvances} simulated seconds, so it is not bounded.");

		_ = await Should.ThrowAsync<NpgsqlException>(async () => await operation.ConfigureAwait(true)).ConfigureAwait(true);
	}

	private static PostgresDataRequestRetryPolicy CreatePolicy(
		int maxRetryAttempts,
		ILogger logger,
		TimeProvider timeProvider,
		int baseDelayMilliseconds = 1_000,
		bool useJitter = true)
	{
		var options = new PostgresPersistenceOptions();
		options.Resilience.MaxRetryAttempts = maxRetryAttempts;
		options.Resilience.RetryDelayMilliseconds = baseDelayMilliseconds;
		options.Resilience.UseJitter = useJitter;

		return new PostgresDataRequestRetryPolicy(options, logger, timeProvider);
	}

	private static NpgsqlException TransientNpgsqlFailure() => new("boom", new SocketException());

	private static Task<int> DriveAsync(PostgresDataRequestRetryPolicy policy, ThrowingDataRequest request, FakeTimeProvider clock) =>
		PumpAsync(
			policy.ResolveAsync<object, int>(request, () => Task.FromResult(new object()), TestContext.Current.CancellationToken),
			clock);

	/// <summary>
	/// Releases the pipeline's backoff waits by advancing the injected clock until the operation settles. Advancing the clock is the
	/// only thing that lets a wait complete, so no wall-clock time is consumed.
	/// </summary>
	private static async Task<int> PumpAsync(Task<int> operation, FakeTimeProvider clock)
	{
		for (var i = 0; i < 500 && !operation.IsCompleted; i++)
		{
			await Task.Yield();
			clock.Advance(TimeSpan.FromSeconds(1));
		}

		return await operation;
	}

	/// <summary>
	/// Captures the delay reported by each retry log entry, so the backoff schedule can be asserted.
	/// </summary>
	private sealed class CapturingLogger : ILogger
	{
		public List<double> RetryDelaysMilliseconds { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
			{
				return;
			}

			foreach (var value in values)
			{
				if (string.Equals(value.Key, "DelayMs", StringComparison.Ordinal) && value.Value is double delay)
				{
					RetryDelaysMilliseconds.Add(delay);
				}
			}
		}
	}

	/// <summary>
	/// A database failure that is not an Npgsql one, so the message-based transience fallback is exercised on its own rather than
	/// through the driver's IsTransient signal.
	/// </summary>
	private sealed class TestDbException(string message) : DbException(message);

	private static class DataRequest
	{
		public static ThrowingDataRequest AlwaysThrowing(Func<Exception> exceptionFactory) =>
			new(exceptionFactory, throwUntilInvocation: int.MaxValue, result: 0);

		public static ThrowingDataRequest ThrowingUntil(int failingInvocations, Func<Exception> exceptionFactory, int result) =>
			new(exceptionFactory, failingInvocations, result);
	}

	private sealed class ThrowingDataRequest(Func<Exception> exceptionFactory, int throwUntilInvocation, int result)
		: IDataRequest<object, int>
	{
		public int Invocations { get; private set; }

		public string RequestId { get; } = Guid.NewGuid().ToString();

		public string RequestType => nameof(ThrowingDataRequest);

		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UnixEpoch;

		public string? CorrelationId => null;

		public IDictionary<string, object>? Metadata => null;

		public CommandDefinition Command => default;

		public DynamicParameters Parameters { get; } = new();

		public Func<object, Task<int>> ResolveAsync => _ =>
		{
			Invocations++;
			return Invocations <= throwUntilInvocation
				? Task.FromException<int>(exceptionFactory())
				: Task.FromResult(result);
		};
	}
}
