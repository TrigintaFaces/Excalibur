// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Resilience;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Polly;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Provides factory methods for creating data access policies with Polly resilience patterns for SQL Server operations.
/// </summary>
public partial class SqlDataAccessPolicyFactory : IDataAccessPolicyFactory
{
	private readonly ILogger<SqlDataAccessPolicyFactory> _logger;
	private readonly IAsyncPolicy _cachedRetryPolicy;
	private readonly IAsyncPolicy _cachedComprehensivePolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlDataAccessPolicyFactory" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance for logging policy events. </param>
	public SqlDataAccessPolicyFactory(ILogger<SqlDataAccessPolicyFactory> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cachedRetryPolicy = CreateWaitAndRetryPolicy();
		_cachedComprehensivePolicy = Policy.WrapAsync(CreateCircuitBreakerPolicy(), _cachedRetryPolicy);
	}

	/// <summary>
	/// Gets a comprehensive policy that combines retry and circuit breaker policies.
	/// </summary>
	/// <remarks>
	/// The wrapped policy is built once and shared. A circuit breaker carries the failure state that
	/// decides whether the circuit is open, so a breaker constructed per call starts closed every time
	/// and can never trip -- the breaker half of this policy would be decorative. Sharing one instance
	/// is what makes the circuit actually break, and it removes a per-call allocation on a data-access
	/// hot path as a side effect rather than as the point.
	/// </remarks>
	/// <returns> An asynchronous policy for comprehensive data access resilience. </returns>
	public IAsyncPolicy GetComprehensivePolicy() => _cachedComprehensivePolicy;

	/// <summary>
	/// Gets a retry policy for transient failures.
	/// </summary>
	/// <returns> An asynchronous retry policy for data access operations. </returns>
	public IAsyncPolicy GetRetryPolicy() => _cachedRetryPolicy;

	/// <summary>
	/// Creates a circuit breaker policy for handling repeated failures.
	/// </summary>
	/// <returns> An asynchronous circuit breaker policy for data access operations. </returns>
	public IAsyncPolicy CreateCircuitBreakerPolicy() =>
		Policy
			.Handle<SqlException>(IsTransientSqlException)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
			.AdvancedCircuitBreakerAsync(
				failureThreshold: 0.5,
				samplingDuration: TimeSpan.FromSeconds(60),
				minimumThroughput: 5,
				durationOfBreak: TimeSpan.FromSeconds(30),
				onBreak: (exception, duration) => LogCircuitBreakerOpened(duration, exception.Message, exception),
				onReset: () => LogCircuitBreakerReset());

	/// <summary>
	/// Determines whether the specified SQL exception is transient and can be retried.
	/// </summary>
	/// <param name="ex"> The SQL exception to evaluate. </param>
	/// <returns> <c> true </c> if the exception is transient; otherwise, <c> false </c>. </returns>
	private static bool IsTransientSqlException(SqlException ex) =>

		// Common transient SQL Server error numbers that warrant retry
		ex.Number switch
		{
			-2 => true, // Timeout expired
			2 => true, // Connection timeout
			53 => true, // Network path not found
			121 => true, // Semaphore timeout expired
			1205 => true, // Deadlock victim
			1222 => true, // Lock request timeout
			8645 => true, // Memory/resource timeout
			8651 => true, // Low memory condition
			10053 => true, // Connection broken
			10054 => true, // Connection reset by peer
			10060 => true, // Connection timeout
			40197 => true, // Service busy (Azure SQL)
			40501 => true, // Service busy (Azure SQL)
			40613 => true, // Database unavailable (Azure SQL)
			49918 => true, // Cannot process request (Azure SQL)
			49919 => true, // Cannot process create/update request (Azure SQL)
			49920 => true, // Cannot process request (Azure SQL)
			_ => false,
		};

	/// <summary>
	/// The retry schedule: two seconds, doubling, capped at thirty, with full jitter below the cap.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The framework's own calculator rather than a sixth hand-rolled formula.</b> The provider here used
	/// to compute <c>TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + Random.Shared.Next(0, 100)
	/// milliseconds</c>. That adds at most 100ms to a delay measured in seconds -- about 5% at the first
	/// retry and 1.25% at the third -- so two hosts that failed in the same instant retried within a tenth
	/// of a second of each other, every time. It was a rounding error wearing the word jitter. It was also
	/// one-sided, so it could only make a client slower, never spread the herd, and it had no ceiling at
	/// all: the schedule was bounded only by the retry count.
	/// </para>
	/// <para>
	/// Polly's own <c>UseJitter</c> is not reachable from here. This type returns
	/// <see cref="IAsyncPolicy" /> -- the Polly v7 API -- which has no jitter switch, because in v7 the
	/// jitter IS the sleep-duration provider, so something must compute it. Rather than invent a
	/// distribution, it delegates to the single implementation the framework already has, which caps first
	/// and randomises below the capped value. Moving this type to Polly v8's <c>ResiliencePipeline</c> would
	/// let the dependency own the schedule outright, but <see cref="IDataAccessPolicyFactory" /> is public
	/// and returns <see cref="IAsyncPolicy" />, so that is a public break across five packages and twelve
	/// call sites -- a separate decision, deliberately not taken here.
	/// </para>
	/// <para>
	/// The nominal ladder is unchanged. Polly v7 passes a 1-based attempt, and a two-second base doubling
	/// gives 2s, 4s, 8s across the three retries exactly as before. The thirty-second ceiling does not bind
	/// at three retries; it is a real bound that starts mattering if the budget grows, which is why it is
	/// stated rather than left implicit at infinity.
	/// </para>
	/// </remarks>
	internal static readonly IBackoffCalculator RetrySchedule = BackoffCalculatorFactory.Create(
		BackoffStrategy.Exponential,
		new BackoffParameters
		{
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromSeconds(30),
			Multiplier = 2.0,
		},
		useJitter: true);

	/// <summary>
	/// Creates a wait and retry policy for handling transient SQL Server failures.
	/// </summary>
	/// <returns> An asynchronous retry policy with exponential backoff and full jitter. </returns>
	private IAsyncPolicy CreateWaitAndRetryPolicy() =>
		Policy
			.Handle<SqlException>(IsTransientSqlException)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
			.WaitAndRetryAsync(
				retryCount: 3,
				sleepDurationProvider: RetrySchedule.CalculateDelay,
				onRetry: (outcome, timespan, retryCount, context) =>
					LogSqlOperationRetry(retryCount, timespan.TotalMilliseconds));

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.CircuitBreakerOpened, LogLevel.Warning,
		"Circuit breaker opened for {Duration} due to: {ExceptionMessage}")]
	private partial void LogCircuitBreakerOpened(TimeSpan duration, string exceptionMessage, Exception ex);

	[LoggerMessage(DataSqlServerEventId.CircuitBreakerReset, LogLevel.Information,
		"Circuit breaker reset - resuming normal operations")]
	private partial void LogCircuitBreakerReset();

	[LoggerMessage(DataSqlServerEventId.SqlOperationRetry, LogLevel.Warning,
		"Retry {RetryCount} for SQL operation after {Delay}ms")]
	private partial void LogSqlOperationRetry(int retryCount, double delay);
}
