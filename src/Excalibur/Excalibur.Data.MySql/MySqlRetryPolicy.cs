// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.MySql.Diagnostics;

using Microsoft.Extensions.Logging;

using MySqlConnector;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.MySql;

/// <summary>
/// MySQL-specific retry policy implementation for handling transient failures.
/// </summary>
public sealed partial class MySqlRetryPolicy : IDataRequestRetryPolicy
{
	/// <summary>
	/// MySQL error code: Too many connections.
	/// </summary>
	private const int ErrorTooManyConnections = 1040;

	/// <summary>
	/// MySQL error code: Lock wait timeout exceeded.
	/// </summary>
	private const int ErrorLockWaitTimeout = 1205;

	/// <summary>
	/// MySQL error code: Deadlock found when trying to get lock.
	/// </summary>
	private const int ErrorDeadlock = 1213;

	/// <summary>
	/// MySQL error code: Can't connect to MySQL server.
	/// </summary>
	private const int ErrorCannotConnect = 2002;

	/// <summary>
	/// MySQL error code: Can't connect to MySQL server (TCP).
	/// </summary>
	private const int ErrorCannotConnectTcp = 2003;

	/// <summary>
	/// MySQL error code: Lost connection to MySQL server.
	/// </summary>
	private const int ErrorLostConnection = 2013;

	/// <summary>
	/// MySQL error code: MySQL server has gone away.
	/// </summary>
	private const int ErrorServerGoneAway = 2006;

	private static readonly int[] TransientErrorCodes =
	[
		ErrorTooManyConnections,
		ErrorLockWaitTimeout,
		ErrorDeadlock,
		ErrorCannotConnect,
		ErrorCannotConnectTcp,
		ErrorLostConnection,
		ErrorServerGoneAway,
	];

	private readonly ILogger _logger;
	private readonly AsyncRetryPolicy _retryPolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlRetryPolicy"/> class.
	/// </summary>
	/// <param name="options">The MySQL provider options.</param>
	/// <param name="logger">The logger instance.</param>
	public MySqlRetryPolicy(MySqlProviderOptions options, ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		MaxRetryAttempts = options.MaxRetryCount;
		BaseRetryDelay = TimeSpan.FromSeconds(1);

		_retryPolicy = Policy
			.Handle<MySqlException>(IsTransientError)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
			.WaitAndRetryAsync(
				MaxRetryAttempts,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogRetryAttempt(retryCount, timeSpan.TotalMilliseconds, exception));
	}

	/// <inheritdoc/>
	public int MaxRetryAttempts { get; }

	/// <inheritdoc/>
	public TimeSpan BaseRetryDelay { get; }

	/// <inheritdoc/>
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		return await _retryPolicy.ExecuteAsync(
			async ct =>
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				try
				{
					return await request.ResolveAsync(connection).ConfigureAwait(false);
				}
				finally
				{
					if (connection is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
			}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		return await _retryPolicy.ExecuteAsync(
			async ct =>
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				try
				{
					return await request.ResolveAsync(connection).ConfigureAwait(false);
				}
				finally
				{
					if (connection is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
			}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public bool ShouldRetry(Exception exception) =>
		exception switch
		{
			MySqlException mySqlEx => IsTransientError(mySqlEx),
			TimeoutException => true,
			InvalidOperationException ioEx when ioEx.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) => true,
			_ => false,
		};

	/// <summary>
	/// Determines if a MySQL exception represents a transient error.
	/// </summary>
	/// <param name="exception">The MySQL exception to evaluate.</param>
	/// <returns><see langword="true"/> if the error is transient; otherwise, <see langword="false"/>.</returns>
	private static bool IsTransientError(MySqlException exception) =>
		TransientErrorCodes.Contains(exception.Number);

	[LoggerMessage(DataMySqlEventId.RetryAttempt, LogLevel.Warning,
		"MySQL operation failed with transient error. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogRetryAttempt(int retryCount, double timeSpan, Exception ex);
}
