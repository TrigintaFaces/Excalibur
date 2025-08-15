// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Data;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Polly;

namespace Excalibur.DataAccess.SqlServer;

public class SqlDataAccessPolicyFactory : IDataAccessPolicyFactory
{
	private readonly ILogger<SqlDataAccessPolicyFactory> _logger;

	private readonly IAsyncPolicy _cachedRetryPolicy;

	public SqlDataAccessPolicyFactory(ILogger<SqlDataAccessPolicyFactory> logger)
	{
		_logger = logger;

		_cachedRetryPolicy = CreateWaitAndRetryPolicy();
	}

	public IAsyncPolicy GetComprehensivePolicy() => Policy.WrapAsync(CreateCircuitBreakerPolicy(), GetRetryPolicy());

	public IAsyncPolicy GetRetryPolicy() => _cachedRetryPolicy;

	public IAsyncPolicy CreateCircuitBreakerPolicy() =>
		Policy.Handle<SqlException>().Or<TimeoutException>().AdvancedCircuitBreakerAsync(
			failureThreshold: 0.5, // Break if >50% actions result in a failure
			samplingDuration: TimeSpan.FromMinutes(1), // Sample failures over a 1-minute period
			minimumThroughput: 5, // At least 5 actions must be processed in the sampling duration
			durationOfBreak: TimeSpan.FromMinutes(2), // Break for 2 minutes
			onBreak: (Exception ex, TimeSpan breakDelay) =>
			{
				switch (ex)
				{
					case SqlException sqlEx:
						_logger.LogWarning(
							"Advanced circuit breaker opened for {BreakDelay} due to SQL error {ErrorNumber}: {Message}",
							breakDelay,
							sqlEx.Number,
							sqlEx.Message);
						break;

					case TimeoutException timeoutEx:
						_logger.LogWarning(
							"Advanced circuit breaker opened for {BreakDelay} due to Timeout: {Message}",
							breakDelay,
							timeoutEx.Message);
						break;

					default:
						_logger.LogWarning(
							"Advanced circuit breaker opened for {BreakDelay} due to {ExceptionType}: {Message}",
							breakDelay,
							ex.GetType().Name,
							ex.Message);
						break;
				}
			},
			onReset: () => _logger.LogInformation("Advanced circuit breaker reset."),
			onHalfOpen: () => _logger.LogInformation("Advanced circuit breaker is half-open; testing next action."));

	private IAsyncPolicy CreateWaitAndRetryPolicy() =>
		Policy.Handle<SqlException>().Or<TimeoutException>().WaitAndRetryAsync(
			retryCount: 5,
			sleepDurationProvider: (int retryAttempt, Context context) =>
			{
				if (context.TryGetValue("LastSqlException", out var lastSqlException) && lastSqlException is SqlException { Number: 927 })
				{
					// Special case: Wait 20 minutes for restore error (927)
					return TimeSpan.FromMinutes(20);
				}

				// Exponential backoff for other transient errors
				return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
			},
			onRetry: (Exception exception, TimeSpan timeSpan, int retryCount, Context context) =>
			{
				if (exception is SqlException sqlException)
				{
					context["LastSqlException"] = exception;

					if (sqlException.Number is 596 or -2
						&& context.TryGetValue("DbConnection", out var conn)
						&& conn is IDbConnection dbConnection)
					{
						try
						{
							dbConnection.Close();
							dbConnection.Open();
						}
						catch (Exception resetEx)
						{
							_logger.LogError(resetEx, "Failed to reset SQL connection on retry.");
						}
					}

					_logger.LogWarning(
						"Retry {RetryCount} due to SQL error: {ErrorNumber}:{Message}",
						retryCount,
						sqlException.Number,
						exception.Message);
				}
				else if (exception is TimeoutException timeoutException)
				{
					if (context.TryGetValue("DbConnection", out var connectionObj)
						&& connectionObj is IDbConnection dbConnection)
					{
						try
						{
							dbConnection.Close();
							dbConnection.Open();
						}
						catch (Exception resetEx)
						{
							_logger.LogError(resetEx, "Failed to reset SQL connection on retry.");
						}
					}

					_logger.LogWarning(
						"Retry {RetryCount} due to Timeout: {Message}",
						retryCount,
						timeoutException.Message);
				}

				_logger.LogWarning("Waiting {TimeSpan} before next retry.", timeSpan);
			});
}
