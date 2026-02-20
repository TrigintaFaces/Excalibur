// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.SqlServer.Diagnostics;

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

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlDataAccessPolicyFactory" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance for logging policy events. </param>
	public SqlDataAccessPolicyFactory(ILogger<SqlDataAccessPolicyFactory> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cachedRetryPolicy = CreateWaitAndRetryPolicy();
	}

	/// <summary>
	/// Gets a comprehensive policy that combines retry and circuit breaker policies.
	/// </summary>
	/// <returns> An asynchronous policy for comprehensive data access resilience. </returns>
	public IAsyncPolicy GetComprehensivePolicy() => Policy.WrapAsync(CreateCircuitBreakerPolicy(), GetRetryPolicy());

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
	/// Creates a wait and retry policy for handling transient SQL Server failures.
	/// </summary>
	/// <returns> An asynchronous retry policy with exponential backoff. </returns>
	private IAsyncPolicy CreateWaitAndRetryPolicy() =>
		Policy
			.Handle<SqlException>(IsTransientSqlException)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
			.WaitAndRetryAsync(
				retryCount: 3,
				sleepDurationProvider: retryAttempt =>

					// Jitter for retry backoff doesn't require cryptographic randomness - it's for load distribution, not security
					// R0.8: Do not use insecure randomness
#pragma warning disable CA5394
					TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
#pragma warning restore CA5394
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
