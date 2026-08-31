// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MySql.Diagnostics;
using Excalibur.Data.Resilience;

using Microsoft.Extensions.Logging;

using MySqlConnector;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.MySql;

/// <summary>
/// MySQL-specific retry policy implementation for handling transient failures.
/// </summary>
public sealed partial class MySqlRetryPolicy : IRelationalDataRequestRetryPolicy, IDocumentDataRequestRetryPolicy
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
				CalculateDelay,
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogRetryAttempt(retryCount, timeSpan.TotalMilliseconds, exception));
	}

	/// <summary>
	/// Gets the ceiling on any single backoff delay.
	/// </summary>
	/// <value> The ceiling on any single backoff delay. Thirty seconds, matching the other providers. </value>
	private static TimeSpan MaxRetryDelay => TimeSpan.FromSeconds(30);

	/// <inheritdoc/>
	public int MaxRetryAttempts { get; }

	/// <inheritdoc/>
	public TimeSpan BaseRetryDelay { get; }

	/// <summary>
	/// Calculates the backoff delay before a retry attempt, grown exponentially from
	/// <see cref="BaseRetryDelay" /> and bounded by <see cref="MaxRetryDelay" />.
	/// </summary>
	/// <param name="retryAttempt"> The retry attempt number (1-based). </param>
	/// <returns> The delay to wait before retrying, never exceeding <see cref="MaxRetryDelay" />. </returns>
	/// <remarks>
	/// <para>
	/// The schedule is grown from the configured base delay. It previously ignored
	/// <see cref="BaseRetryDelay" /> entirely and returned two raised to the attempt number, in seconds -
	/// so the property this type advertises as the base of its backoff described a schedule it was not
	/// using, and the delay had no ceiling at all.
	/// </para>
	/// <para>
	/// Composed with a minimum, so the ceiling can only ever tighten the wait; relaxing it requires
	/// turning the minimum into a maximum rather than any ordinary edit.
	/// </para>
	/// </remarks>
	internal TimeSpan CalculateDelay(int retryAttempt)
	{
		var exponential = BaseRetryDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1);
		return TimeSpan.FromMilliseconds(Math.Min(exponential, MaxRetryDelay.TotalMilliseconds));
	}

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
