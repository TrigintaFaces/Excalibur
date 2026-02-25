// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Jobs.Coordination;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.SqlServer;

/// <summary>
/// SQL Server-based implementation of <see cref="IJobCoordinator"/> for distributed job coordination.
/// Uses Dapper for all data access operations.
/// </summary>
public sealed partial class SqlServerJobCoordinator : IJobCoordinator
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly SqlServerJobCoordinatorOptions _options;
	private readonly ILogger<SqlServerJobCoordinator> _logger;
	private readonly string _schema;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerJobCoordinator"/> class.
	/// </summary>
	/// <param name="options">The coordinator configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerJobCoordinator(
		IOptions<SqlServerJobCoordinatorOptions> options,
		ILogger<SqlServerJobCoordinator> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_schema = _options.SchemaName;
		_connectionFactory = () => new SqlConnection(_options.ConnectionString);
	}

	/// <inheritdoc />
	public async Task<IDistributedJobLock?> TryAcquireLockAsync(
		string jobKey,
		TimeSpan lockDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);

		var instanceId = Environment.MachineName + "_" + Environment.ProcessId;
		var now = DateTimeOffset.UtcNow;
		var expiresAt = now.Add(lockDuration);

		// Attempt to insert the lock row; if it already exists and hasn't expired, skip.
		// If an expired row exists for the same key, delete it first.
		var cleanupSql = $"""
			DELETE FROM [{_schema}].[Locks]
			WHERE [JobKey] = @JobKey AND [ExpiresAt] < @Now
			""";

		var insertSql = $"""
			INSERT INTO [{_schema}].[Locks] ([JobKey], [InstanceId], [AcquiredAt], [ExpiresAt])
			SELECT @JobKey, @InstanceId, @AcquiredAt, @ExpiresAt
			WHERE NOT EXISTS (
				SELECT 1 FROM [{_schema}].[Locks] WHERE [JobKey] = @JobKey
			)
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Clean up expired locks first
		_ = await connection.ExecuteAsync(cleanupSql, new { JobKey = jobKey, Now = now }).ConfigureAwait(false);

		// Attempt to acquire
		var rowsAffected = await connection.ExecuteAsync(
			insertSql,
			new { JobKey = jobKey, InstanceId = instanceId, AcquiredAt = now, ExpiresAt = expiresAt }).ConfigureAwait(false);

		if (rowsAffected > 0)
		{
			LogLockAcquired(jobKey, instanceId);
			return new SqlServerDistributedJobLock(
				_connectionFactory, _schema, jobKey, instanceId, now, expiresAt, _logger);
		}

		LogLockAcquisitionFailed(jobKey, instanceId);
		return null;
	}

	/// <inheritdoc />
	public async Task RegisterInstanceAsync(
		string instanceId,
		JobInstanceInfo instanceInfo,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
		ArgumentNullException.ThrowIfNull(instanceInfo);

		var data = JsonSerializer.Serialize(instanceInfo, SqlServerJobCoordinatorSerializerContext.Default.JobInstanceInfo);

		var sql = $"""
			MERGE [{_schema}].[Instances] AS target
			USING (SELECT @InstanceId AS InstanceId) AS source
			ON target.[InstanceId] = source.InstanceId
			WHEN MATCHED THEN
				UPDATE SET [HostName] = @HostName, [Data] = @Data, [HeartbeatAt] = @HeartbeatAt
			WHEN NOT MATCHED THEN
				INSERT ([InstanceId], [HostName], [Data], [HeartbeatAt], [RegisteredAt])
				VALUES (@InstanceId, @HostName, @Data, @HeartbeatAt, @RegisteredAt);
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(sql, new
		{
			InstanceId = instanceId,
			instanceInfo.HostName,
			Data = data,
			HeartbeatAt = DateTimeOffset.UtcNow,
			RegisteredAt = DateTimeOffset.UtcNow
		}).ConfigureAwait(false);

		LogInstanceRegistered(instanceId, instanceInfo.HostName);
	}

	/// <inheritdoc />
	public async Task UnregisterInstanceAsync(string instanceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

		var sql = $"""
			DELETE FROM [{_schema}].[Instances]
			WHERE [InstanceId] = @InstanceId
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(sql, new { InstanceId = instanceId }).ConfigureAwait(false);

		LogInstanceUnregistered(instanceId);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<JobInstanceInfo>> GetActiveInstancesAsync(CancellationToken cancellationToken)
	{
		var heartbeatCutoff = DateTimeOffset.UtcNow - _options.InstanceTtl;

		// Clean up stale instances and return active ones
		var cleanupSql = $"""
			DELETE FROM [{_schema}].[Instances]
			WHERE [HeartbeatAt] < @HeartbeatCutoff
			""";

		var selectSql = $"""
			SELECT [Data]
			FROM [{_schema}].[Instances]
			WHERE [HeartbeatAt] >= @HeartbeatCutoff
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(cleanupSql, new { HeartbeatCutoff = heartbeatCutoff }).ConfigureAwait(false);

		var rows = await connection.QueryAsync<string>(selectSql, new { HeartbeatCutoff = heartbeatCutoff }).ConfigureAwait(false);

		var instances = new List<JobInstanceInfo>();
		foreach (var row in rows)
		{
			try
			{
				var info = JsonSerializer.Deserialize(row, SqlServerJobCoordinatorSerializerContext.Default.JobInstanceInfo);
				if (info is not null)
				{
					instances.Add(info);
				}
			}
			catch (JsonException ex)
			{
				LogInstanceDeserializationFailed(ex);
			}
		}

		return instances;
	}

	/// <inheritdoc />
	public async Task<string?> DistributeJobAsync(
		string jobKey,
		object jobData,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);

		var activeInstances = await GetActiveInstancesAsync(cancellationToken).ConfigureAwait(false);
		var availableInstance = activeInstances
			.Where(static i => i.IsHealthy(TimeSpan.FromMinutes(2)) &&
				i.ActiveJobCount < i.Capabilities.MaxConcurrentJobs)
			.OrderBy(static i => i.ActiveJobCount)
			.ThenByDescending(static i => i.Capabilities.Priority)
			.FirstOrDefault();

		if (availableInstance is null)
		{
			LogNoInstanceAvailable(jobKey);
			return null;
		}

		var serializedData = JsonSerializer.Serialize(jobData, SqlServerJobCoordinatorSerializerContext.Default.JsonElement);

		var sql = $"""
			INSERT INTO [{_schema}].[Queue] ([JobKey], [AssignedInstance], [JobData], [CreatedAt], [Status])
			VALUES (@JobKey, @AssignedInstance, @JobData, @CreatedAt, 0)
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(sql, new
		{
			JobKey = jobKey,
			AssignedInstance = availableInstance.InstanceId,
			JobData = serializedData,
			CreatedAt = DateTimeOffset.UtcNow
		}).ConfigureAwait(false);

		LogJobDistributed(jobKey, availableInstance.InstanceId);
		return availableInstance.InstanceId;
	}

	/// <inheritdoc />
	public async Task ReportJobCompletionAsync(
		string jobKey,
		string instanceId,
		bool success,
		object? result,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

		string? resultData = null;
		if (result is not null)
		{
			resultData = JsonSerializer.Serialize(
				JsonSerializer.SerializeToElement(result),
				SqlServerJobCoordinatorSerializerContext.Default.JsonElement);
		}

		var sql = $"""
			INSERT INTO [{_schema}].[Completions] ([JobKey], [InstanceId], [Success], [ResultData], [CompletedAt])
			VALUES (@JobKey, @InstanceId, @Success, @ResultData, @CompletedAt)
			""";

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		_ = await connection.ExecuteAsync(sql, new
		{
			JobKey = jobKey,
			InstanceId = instanceId,
			Success = success,
			ResultData = resultData,
			CompletedAt = DateTimeOffset.UtcNow
		}).ConfigureAwait(false);

		LogJobCompletionReported(jobKey, instanceId, success ? "Success" : "Failed");
	}

	[LoggerMessage(147420, LogLevel.Debug,
		"Acquired SQL Server distributed lock for job {JobKey} by instance {InstanceId}")]
	private partial void LogLockAcquired(string jobKey, string instanceId);

	[LoggerMessage(147421, LogLevel.Debug,
		"Failed to acquire SQL Server distributed lock for job {JobKey} by instance {InstanceId}")]
	private partial void LogLockAcquisitionFailed(string jobKey, string instanceId);

	[LoggerMessage(147422, LogLevel.Information,
		"Registered job processing instance {InstanceId} on host {HostName}")]
	private partial void LogInstanceRegistered(string instanceId, string hostName);

	[LoggerMessage(147423, LogLevel.Information,
		"Unregistered job processing instance {InstanceId}")]
	private partial void LogInstanceUnregistered(string instanceId);

	[LoggerMessage(147424, LogLevel.Warning,
		"Failed to deserialize instance info from SQL Server")]
	private partial void LogInstanceDeserializationFailed(Exception exception);

	[LoggerMessage(147425, LogLevel.Debug,
		"Distributed job {JobKey} to instance {InstanceId}")]
	private partial void LogJobDistributed(string jobKey, string instanceId);

	[LoggerMessage(147426, LogLevel.Warning,
		"No available instances found to process job {JobKey}")]
	private partial void LogNoInstanceAvailable(string jobKey);

	[LoggerMessage(147427, LogLevel.Debug,
		"Reported completion for job {JobKey} by instance {InstanceId}: {Status}")]
	private partial void LogJobCompletionReported(string jobKey, string instanceId, string status);
}
