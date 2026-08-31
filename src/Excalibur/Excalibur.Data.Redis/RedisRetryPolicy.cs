// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Redis.Diagnostics;
using Excalibur.Data.Resilience;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

using StackExchange.Redis;

namespace Excalibur.Data.Redis;

/// <summary>
/// Redis retry policy implementation.
/// </summary>
/// <remarks>
/// Attempt sequencing, backoff and the terminal rethrow are owned by a Polly <see cref="ResiliencePipeline" />; this type contributes
/// only the Redis-specific policy - which exceptions are transient (<see cref="ShouldRetry(Exception)" />) and how long to wait
/// (<see cref="GetDelay(int)" />). Delays are scheduled through an injected <see cref="TimeProvider" /> so the backoff schedule is
/// observable in a test without elapsing wall-clock time.
/// </remarks>
internal sealed partial class RedisRetryPolicy : IRelationalDataRequestRetryPolicy, IDocumentDataRequestRetryPolicy
{
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _pipeline;
	private readonly ResiliencePipeline _documentPipeline;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisRetryPolicy" /> class scheduling backoff against the system clock.
	/// </summary>
	/// <param name="maxRetryAttempts"> The maximum number of retry attempts made after the initial attempt. </param>
	/// <param name="logger"> The logger instance. </param>
	public RedisRetryPolicy(int maxRetryAttempts, ILogger logger)
		: this(maxRetryAttempts, logger, TimeProvider.System)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisRetryPolicy" /> class scheduling backoff against the supplied time provider.
	/// </summary>
	/// <param name="maxRetryAttempts"> The maximum number of retry attempts made after the initial attempt. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="timeProvider"> The time provider used to schedule the backoff delay between attempts. </param>
	public RedisRetryPolicy(int maxRetryAttempts, ILogger logger, TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(timeProvider);

		_logger = logger;
		MaxRetryAttempts = maxRetryAttempts;

		_pipeline = BuildPipeline(
			timeProvider,
			(exception, attempt, delayMilliseconds) => LogRetryWarning(_logger, exception, attempt, MaxRetryAttempts, delayMilliseconds));

		_documentPipeline = BuildPipeline(
			timeProvider,
			(exception, attempt, delayMilliseconds) => LogDocumentRetryWarning(_logger, exception, attempt, MaxRetryAttempts, delayMilliseconds));
	}

	/// <inheritdoc />
	public static TimeSpan InitialDelay => TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public static TimeSpan MaxDelay => TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	public int MaxRetryAttempts { get; }

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay => TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception, int attemptNumber) =>
		attemptNumber <= MaxRetryAttempts &&
		exception is RedisException or RedisTimeoutException or RedisConnectionException;

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception) =>
		exception is RedisException or RedisTimeoutException or RedisConnectionException;

	/// <inheritdoc />
	public TimeSpan GetDelay(int attemptNumber)
	{
		var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
		return delay > MaxDelay ? MaxDelay : delay;
	}

	/// <inheritdoc />
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken) =>
		await _pipeline.ExecuteAsync(
			async ct =>
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				return await DataRequestExtensions.ResolveAsync(request, connection, ct).ConfigureAwait(false);
			},
			cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken) =>
		await _documentPipeline.ExecuteAsync(
			async ct =>
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				return await DataRequestExtensions.ResolveAsync(request, connection, ct).ConfigureAwait(false);
			},
			cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Builds the retry pipeline that owns attempt sequencing, backoff scheduling and the terminal rethrow.
	/// </summary>
	/// <param name="timeProvider"> The time provider used to schedule the backoff delay. </param>
	/// <param name="onRetry"> Invoked before each backoff with the failure, the 1-based retry number and the delay in milliseconds. </param>
	/// <returns> The configured pipeline. </returns>
	private ResiliencePipeline BuildPipeline(TimeProvider timeProvider, Action<Exception, int, double> onRetry)
	{
		// A non-positive budget means "attempt once, never retry". The retry strategy requires a budget of at least
		// one, so the no-retry case is an empty pipeline rather than a retry strategy configured to do nothing.
		if (MaxRetryAttempts <= 0)
		{
			return ResiliencePipeline.Empty;
		}

		return new ResiliencePipelineBuilder { TimeProvider = timeProvider }
			.AddRetry(new RetryStrategyOptions
			{
				MaxRetryAttempts = MaxRetryAttempts,
				ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is { } exception && ShouldRetry(exception)),

				// AttemptNumber is 0-based on the first failure, so the delay for that failure is GetDelay(1).
				DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(GetDelay(args.AttemptNumber + 1)),
				OnRetry = args =>
				{
					onRetry(args.Outcome.Exception!, args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
					return ValueTask.CompletedTask;
				},
			})
			.Build();
	}

	[LoggerMessage(DataRedisEventId.RetryWarning, LogLevel.Warning, "Redis operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private static partial void LogRetryWarning(ILogger logger, Exception exception, int attempt, int maxAttempts, double delay);

	[LoggerMessage(DataRedisEventId.DocumentRetryWarning, LogLevel.Warning, "Redis document operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private static partial void LogDocumentRetryWarning(ILogger logger, Exception exception, int attempt, int maxAttempts, double delay);
}
