// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Jobs.Coordination;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.SqlServer;

/// <summary>
/// SQL Server-based implementation of <see cref="IDistributedJobLock"/>.
/// </summary>
internal sealed partial class SqlServerDistributedJobLock : IDistributedJobLock
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _schemaName;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDistributedJobLock"/> class.
	/// </summary>
	/// <param name="connectionFactory">The SQL connection factory.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="jobKey">The job key this lock protects.</param>
	/// <param name="instanceId">The instance that holds this lock.</param>
	/// <param name="acquiredAt">When the lock was acquired.</param>
	/// <param name="expiresAt">When the lock expires.</param>
	/// <param name="logger">The logger instance.</param>
	internal SqlServerDistributedJobLock(
		Func<SqlConnection> connectionFactory,
		string schemaName,
		string jobKey,
		string instanceId,
		DateTimeOffset acquiredAt,
		DateTimeOffset expiresAt,
		ILogger logger)
	{
		_connectionFactory = connectionFactory;
		_schemaName = schemaName;
		JobKey = jobKey;
		InstanceId = instanceId;
		AcquiredAt = acquiredAt;
		ExpiresAt = expiresAt;
		_logger = logger;
	}

	/// <inheritdoc />
	public string JobKey { get; }

	/// <inheritdoc />
	public string InstanceId { get; }

	/// <inheritdoc />
	public DateTimeOffset AcquiredAt { get; }

	/// <inheritdoc />
	public DateTimeOffset ExpiresAt { get; private set; }

	/// <inheritdoc />
	public bool IsValid => !_disposed && DateTimeOffset.UtcNow < ExpiresAt;

	/// <inheritdoc />
	public async Task<bool> ExtendAsync(TimeSpan additionalDuration, CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return false;
		}

		var newExpiresAt = DateTimeOffset.UtcNow.Add(additionalDuration);
		var sql = $"""
			UPDATE [{_schemaName}].[Locks]
			SET [ExpiresAt] = @NewExpiresAt
			WHERE [JobKey] = @JobKey AND [InstanceId] = @InstanceId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var rowsAffected = await connection.ExecuteAsync(sql, new { NewExpiresAt = newExpiresAt, JobKey, InstanceId }).ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			ExpiresAt = newExpiresAt;
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public async Task ReleaseAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		var sql = $"""
			DELETE FROM [{_schemaName}].[Locks]
			WHERE [JobKey] = @JobKey AND [InstanceId] = @InstanceId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(sql, new { JobKey, InstanceId }).ConfigureAwait(false);
		_disposed = true;

		LogLockReleased(_logger, JobKey);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		try
		{
			await ReleaseAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			LogLockReleaseTimedOut(_logger, JobKey);
		}
	}

	[LoggerMessage(147410, LogLevel.Debug, "Released SQL Server distributed lock for job {JobKey}")]
	private static partial void LogLockReleased(ILogger logger, string jobKey);

	[LoggerMessage(147411, LogLevel.Warning, "Timed out releasing SQL Server distributed lock for job {JobKey} during disposal")]
	private static partial void LogLockReleaseTimedOut(ILogger logger, string jobKey);
}
