// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data.Common;
using System.Net.Sockets;

using Excalibur.Data.Resilience;

using Microsoft.Extensions.Logging;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Postgres-specific implementation of DataRequest retry policy that handles Postgres transient failures and provides appropriate retry logic.
/// </summary>
/// <remarks>
/// Attempt sequencing, backoff, jitter, the ceiling and the terminal rethrow are all owned by a Polly
/// <see cref="ResiliencePipeline" />; this type contributes only the Postgres-specific policy - which exceptions are
/// transient (<see cref="ShouldRetry(Exception)" />). It used to contribute the delay as well, through a
/// custom delay generator; that generator is gone and the schedule is now configured on the strategy, so
/// nothing here re-implements what the dependency already provides. Delays are scheduled through an injected
/// <see cref="TimeProvider" /> so the backoff schedule is observable in a test without elapsing wall-clock time.
/// </remarks>
internal sealed class PostgresDataRequestRetryPolicy : IRelationalDataRequestRetryPolicy
{
	private readonly PostgresPersistenceOptions _options;
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _pipeline;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresDataRequestRetryPolicy" /> class scheduling backoff against the system clock.
	/// </summary>
	/// <param name="options"> The Postgres persistence options. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public PostgresDataRequestRetryPolicy(PostgresPersistenceOptions options, ILogger logger)
		: this(options, logger, TimeProvider.System)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresDataRequestRetryPolicy" /> class scheduling backoff against the supplied
	/// time provider.
	/// </summary>
	/// <param name="options"> The Postgres persistence options. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	/// <param name="timeProvider"> The time provider used to schedule the backoff delay between attempts. </param>
	public PostgresDataRequestRetryPolicy(PostgresPersistenceOptions options, ILogger logger, TimeProvider timeProvider)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(timeProvider);

		_pipeline = BuildPipeline(timeProvider);
	}

	/// <inheritdoc />
	public int MaxRetryAttempts => _options.Resilience.MaxRetryAttempts;

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay => TimeSpan.FromMilliseconds(_options.Resilience.RetryDelayMilliseconds);

	/// <inheritdoc />
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Counts executions, not retries, so the success and permanent-failure logs below can report the same
		// attempt arithmetic the pipeline is applying.
		var executions = 0;

		try
		{
			var result = await _pipeline.ExecuteAsync(
				async ct =>
				{
					executions++;
					var connection = await connectionFactory().ConfigureAwait(false);
					return await request.ResolveAsync(connection).ConfigureAwait(false);
				},
				cancellationToken).ConfigureAwait(false);

			if (executions > 1)
			{
				_logger.LogInformation("DataRequest succeeded after {AttemptCount} retry attempts", executions - 1);
			}

			return result;
		}
		catch (Exception ex)
		{
			// Non-retryable exception or max attempts reached
			_logger.LogError(
				ex,
				"DataRequest failed permanently after {AttemptCount} attempts. Error: {ErrorMessage}",
				executions, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception) =>
		exception switch
		{
			// Postgres-specific transient exceptions
			NpgsqlException { IsTransient: true } => true,

			// General database exceptions that might be transient
			DbException dbEx when IsTransientDbException(dbEx) => true,

			// Timeout exceptions are typically retryable
			TimeoutException => true,

			// Socket and network exceptions
			SocketException => true,
			IOException => true,

			// Task cancellation should not be retried
			OperationCanceledException => false,

			// Other exceptions are not retryable by default
			_ => false,
		};

	/// <summary>
	/// Determines if a database exception represents a transient failure.
	/// </summary>
	/// <param name="exception"> The database exception to evaluate. </param>
	/// <returns> True if the exception is transient; otherwise, false. </returns>
	private static bool IsTransientDbException(DbException exception)
	{
		// Check for common transient database error messages or codes
		var message = exception.Message?.ToUpperInvariant() ?? string.Empty;

		return message.Contains("TIMEOUT", StringComparison.Ordinal) ||
			   message.Contains("CONNECTION", StringComparison.Ordinal) ||
			   message.Contains("NETWORK", StringComparison.Ordinal) ||
			   message.Contains("DEADLOCK", StringComparison.Ordinal) ||
			   message.Contains("LOCK TIMEOUT", StringComparison.Ordinal) ||
			   message.Contains("CONNECTION RESET", StringComparison.Ordinal) ||
			   message.Contains("BROKEN PIPE", StringComparison.Ordinal);
	}

	/// <summary>
	/// Gets the ceiling on any single backoff delay.
	/// </summary>
	/// <value> The ceiling on any single backoff delay. </value>
	private TimeSpan MaxRetryDelay => TimeSpan.FromMilliseconds(_options.Resilience.MaxRetryDelayMilliseconds);


	/// <summary>
	/// Builds the retry pipeline that owns attempt sequencing, backoff scheduling and the terminal rethrow.
	/// </summary>
	/// <param name="timeProvider"> The time provider used to schedule the backoff delay. </param>
	/// <returns> The configured pipeline. </returns>
	private ResiliencePipeline BuildPipeline(TimeProvider timeProvider)
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

				// The schedule is Polly's, not ours. This used to supply a DelayGenerator computing
				// BaseRetryDelay * 2^(attempt-1) plus a hand-rolled jitter, which forced the method to
				// re-implement the ceiling as well -- MaxDelay is documented to be ignored when a generator
				// is present, so the generator had to cap its own output. Dropping the generator makes
				// MaxDelay live and hands the growth law, the jitter and the cap to the dependency this
				// package already carries. Exponential here is Delay * 2^AttemptNumber with AttemptNumber
				// 0-based, which is the same ladder the generator produced.
				//
				// UseJitter replaces jitter that only ever ADDED: the old factor was
				// Random.Shared.NextDouble() * 0.1 applied as delay * (1 + f), so every value sat at or
				// above the nominal and nothing ever retried sooner than configured. That spreads no herd
				// -- clients that failed together stayed clustered at the lower bound -- it only made them
				// slower. Polly's jitter reaches the low end of the interval, which is the property jitter
				// exists for.
				BackoffType = DelayBackoffType.Exponential,
				Delay = BaseRetryDelay,
				MaxDelay = MaxRetryDelay,
				UseJitter = true,
				OnRetry = args =>
				{
					var exception = args.Outcome.Exception!;
					_logger.LogWarning(
						exception,
						"DataRequest failed on attempt {AttemptCount}/{MaxAttempts}. Retrying after {DelayMs}ms. Error: {ErrorMessage}",
						args.AttemptNumber + 1, MaxRetryAttempts + 1, args.RetryDelay.TotalMilliseconds, exception.Message);
					return ValueTask.CompletedTask;
				},
			})
			.Build();
	}
}
