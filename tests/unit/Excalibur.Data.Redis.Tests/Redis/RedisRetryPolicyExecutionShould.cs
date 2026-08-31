// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Data.Redis;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Execution-semantics tests for <c>RedisRetryPolicy</c>.
/// </summary>
/// <remarks>
/// These pin the observable contract of the retry execution path - how many times the request is actually invoked, the exact backoff
/// schedule, which exception surfaces when the budget is exhausted, and the success path - independently of which mechanism drives it.
/// Backoff is scheduled against an injected <see cref="FakeTimeProvider" />, so the schedule is asserted without elapsing wall-clock time.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class RedisRetryPolicyExecutionShould : UnitTestBase
{
	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 2)]
	[InlineData(3, 4)]
	[InlineData(5, 6)]
	public async Task InvokeTheRequestOncePlusItsRetryBudget(int maxRetryAttempts, int expectedInvocations)
	{
		var clock = new FakeTimeProvider();
		var policy = new RedisRetryPolicy(maxRetryAttempts, new CapturingLogger(), clock);
		var request = DataRequest.AlwaysThrowing(() => new RedisException("transient"));

		_ = await Should.ThrowAsync<RedisException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(expectedInvocations);
	}

	[Fact]
	public async Task BackOffOnTheExactExponentialScheduleCappedAtMaxDelay()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();

		// A budget of 5 crosses the 30s cap: 2, 4, 8, 16, then 32 capped to 30.
		var policy = new RedisRetryPolicy(5, logger, clock);
		var request = DataRequest.AlwaysThrowing(() => new RedisException("transient"));

		_ = await Should.ThrowAsync<RedisException>(DriveAsync(policy, request, clock));

		logger.RetryDelaysMilliseconds.ShouldBe([2_000d, 4_000d, 8_000d, 16_000d, 30_000d]);
	}

	[Fact]
	public async Task SurfaceTheFinalTransientFailureWhenTheBudgetIsExhausted()
	{
		var clock = new FakeTimeProvider();
		var thrown = new List<RedisException>();
		var policy = new RedisRetryPolicy(2, new CapturingLogger(), clock);
		var request = DataRequest.AlwaysThrowing(() =>
		{
			var exception = new RedisException("transient");
			thrown.Add(exception);
			return exception;
		});

		var surfaced = await Should.ThrowAsync<RedisException>(DriveAsync(policy, request, clock));

		// The exception that surfaces is the one from the LAST attempt, not the first.
		surfaced.ShouldBeSameAs(thrown[^1]);
		thrown.Count.ShouldBe(3);
	}

	[Fact]
	public async Task ReturnTheResultAndStopRetryingOnceTheRequestSucceeds()
	{
		var clock = new FakeTimeProvider();
		var policy = new RedisRetryPolicy(5, new CapturingLogger(), clock);
		var request = DataRequest.ThrowingUntil(2, () => new RedisException("transient"), result: 42);

		var result = await DriveAsync(policy, request, clock);

		result.ShouldBe(42);
		request.Invocations.ShouldBe(3);
	}

	[Fact]
	public async Task PropagateANonTransientFailureWithoutRetrying()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = new RedisRetryPolicy(5, logger, clock);
		var request = DataRequest.AlwaysThrowing(() => new InvalidOperationException("not transient"));

		_ = await Should.ThrowAsync<InvalidOperationException>(DriveAsync(policy, request, clock));

		request.Invocations.ShouldBe(1);
		logger.RetryDelaysMilliseconds.ShouldBeEmpty();
	}

	[Fact]
	public async Task RouteDocumentRequestsThroughTheSameBudgetAndSchedule()
	{
		var clock = new FakeTimeProvider();
		var logger = new CapturingLogger();
		var policy = new RedisRetryPolicy(3, logger, clock);
		var request = new ThrowingDocumentRequest(() => new RedisException("transient"));

		var operation = policy.ResolveDocumentAsync<object, int>(request, () => Task.FromResult(new object()), TestContext.Current.CancellationToken);
		_ = await Should.ThrowAsync<RedisException>(PumpAsync(operation, clock));

		request.Invocations.ShouldBe(4);
		logger.RetryDelaysMilliseconds.ShouldBe([2_000d, 4_000d, 8_000d]);
	}

	private static Task<int> DriveAsync(RedisRetryPolicy policy, ThrowingDataRequest request, FakeTimeProvider clock) =>
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
	/// Captures the delay reported by each retry log entry, so the backoff schedule can be asserted exactly.
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
				if (string.Equals(value.Key, "Delay", StringComparison.Ordinal) && value.Value is double delay)
				{
					RetryDelaysMilliseconds.Add(delay);
				}
			}
		}
	}

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

	private sealed class ThrowingDocumentRequest(Func<Exception> exceptionFactory) : IDocumentDataRequest<object, int>
	{
		public int Invocations { get; private set; }

		public string CollectionName => "test";

		public string OperationType => "Find";

		public IReadOnlyDictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

		public IReadOnlyDictionary<string, object>? Options => null;

		public Func<object, Task<int>> ResolveAsync => _ =>
		{
			Invocations++;
			return Task.FromException<int>(exceptionFactory());
		};
	}
}
