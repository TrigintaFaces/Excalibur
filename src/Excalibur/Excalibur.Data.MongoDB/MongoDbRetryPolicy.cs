// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Data.Resilience;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// MongoDB retry policy implementation.
/// </summary>
/// <remarks>
/// Attempt sequencing, backoff and the terminal rethrow are owned by a Polly <see cref="ResiliencePipeline" />; this type contributes
/// only the MongoDB-specific policy - which exceptions are transient (<see cref="ShouldRetry(Exception)" />) and how long to wait
/// (<see cref="GetDelay(int)" />). Delays are scheduled through an injected <see cref="TimeProvider" /> so the backoff schedule is
/// observable in a test without elapsing wall-clock time.
/// </remarks>
internal sealed partial class MongoDbRetryPolicy : IRelationalDataRequestRetryPolicy, IDocumentDataRequestRetryPolicy
{
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _pipeline;
	private readonly ResiliencePipeline _documentPipeline;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbRetryPolicy" /> class scheduling backoff against the system clock.
	/// </summary>
	/// <param name="maxRetryAttempts"> The maximum number of retry attempts made after the initial attempt. </param>
	/// <param name="logger"> The logger instance. </param>
	public MongoDbRetryPolicy(int maxRetryAttempts, ILogger logger)
		: this(maxRetryAttempts, logger, TimeProvider.System)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbRetryPolicy" /> class scheduling backoff against the supplied time provider.
	/// </summary>
	/// <param name="maxRetryAttempts"> The maximum number of retry attempts made after the initial attempt. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="timeProvider"> The time provider used to schedule the backoff delay between attempts. </param>
	public MongoDbRetryPolicy(int maxRetryAttempts, ILogger logger, TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(timeProvider);

		_logger = logger;
		MaxRetryAttempts = maxRetryAttempts;

		_pipeline = BuildPipeline(
			timeProvider,
			(exception, attempt, delayMilliseconds) => LogMongoOperationRetry(attempt, MaxRetryAttempts, delayMilliseconds, exception));

		_documentPipeline = BuildPipeline(
			timeProvider,
			(exception, attempt, delayMilliseconds) => LogMongoDocumentOperationRetry(attempt, MaxRetryAttempts, delayMilliseconds, exception));
	}

	/// <summary>
	/// Gets the initial delay before the first retry attempt.
	/// </summary>
	/// <value> The initial delay before the first retry attempt. </value>
	public static TimeSpan InitialDelay => TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the maximum delay between retry attempts.
	/// </summary>
	/// <value> The maximum delay between retry attempts. </value>
	public static TimeSpan MaxDelay => TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value> The maximum number of retry attempts. </value>
	public int MaxRetryAttempts { get; }

	/// <summary>
	/// Gets the base delay between retry attempts.
	/// </summary>
	/// <value> The base delay between retry attempts. </value>
	public TimeSpan BaseRetryDelay => TimeSpan.FromSeconds(1);

	/// <summary>
	/// Determines if an exception represents a transient failure that should be retried, considering the current attempt number.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <param name="attemptNumber"> The current retry attempt number. </param>
	/// <returns> True if the exception represents a transient failure and the attempt number is within limits; otherwise, false. </returns>
	public bool ShouldRetry(Exception exception, int attemptNumber) =>
		attemptNumber <= MaxRetryAttempts &&
		exception is MongoException or MongoConnectionException;

	/// <summary>
	/// Determines if an exception represents a transient failure that should be retried.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if the exception represents a transient failure; otherwise, false. </returns>
	public bool ShouldRetry(Exception exception) => exception is MongoException or MongoConnectionException;

	/// <summary>
	/// Calculates the delay before the next retry attempt using exponential backoff.
	/// </summary>
	/// <param name="attemptNumber"> The current retry attempt number. </param>
	/// <returns> The delay to wait before the next retry attempt, capped at the maximum delay. </returns>
	public TimeSpan GetDelay(int attemptNumber)
	{
		var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
		return delay > MaxDelay ? MaxDelay : delay;
	}

	/// <summary>
	/// Executes a DataRequest with retry logic for transient failures.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="connectionFactory"> Factory function to create connections. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
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

	/// <summary>
	/// Executes a document DataRequest with retry logic for transient failures.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The document data request to execute. </param>
	/// <param name="connectionFactory"> Factory function to create connections. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the document data request execution. </returns>
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

	// Source-generated logging methods
	[LoggerMessage(DataMongoDbEventId.MongoOperationRetry, LogLevel.Warning,
		"MongoDB operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private partial void LogMongoOperationRetry(int attempt, int maxAttempts, double delay, Exception? ex);

	[LoggerMessage(DataMongoDbEventId.MongoDocumentOperationRetry, LogLevel.Warning,
		"MongoDB document operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private partial void LogMongoDocumentOperationRetry(int attempt, int maxAttempts, double delay, Exception? ex);
}
