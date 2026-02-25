// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Redis-based implementation of <see cref="IJobCoordinator" /> for distributed job coordination.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RedisJobCoordinator" /> class. </remarks>
/// <param name="database"> The Redis database to use for coordination. </param>
/// <param name="logger"> The logger for this coordinator. </param>
/// <param name="keyPrefix"> Optional prefix for Redis keys to avoid Tests.CloudProviders. </param>
public sealed class RedisJobCoordinator(IDatabase database, ILogger<RedisJobCoordinator> logger, string keyPrefix = "excalibur:jobs:")
	: IJobCoordinator
{
	private readonly IDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
	private readonly ILogger<RedisJobCoordinator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly string _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));

	/// <inheritdoc />
	public async Task<IDistributedJobLock?> TryAcquireLockAsync(string jobKey, TimeSpan lockDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);

		var lockKey = $"{_keyPrefix}locks:{jobKey}";
		var instanceId = Environment.MachineName + "_" + Environment.ProcessId;
		var expiresAt = DateTimeOffset.UtcNow.Add(lockDuration);

		var lockData = JsonSerializer.Serialize(
			new RedisLockData(instanceId, DateTimeOffset.UtcNow, expiresAt),
			RedisJobCoordinatorSerializerContext.Default.RedisLockData);

		var acquired = await _database.StringSetAsync(lockKey, lockData, lockDuration, When.NotExists).ConfigureAwait(false);

		if (acquired)
		{
			_logger.LogDebug("Acquired distributed lock for job {JobKey} by instance {InstanceId}", jobKey, instanceId);
			return new RedisDistributedJobLock(_database, lockKey, jobKey, instanceId, DateTimeOffset.UtcNow, expiresAt, _logger);
		}

		_logger.LogDebug("Failed to acquire distributed lock for job {JobKey} by instance {InstanceId}", jobKey, instanceId);
		return null;
	}

	/// <inheritdoc />
	public async Task RegisterInstanceAsync(string instanceId, JobInstanceInfo instanceInfo, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
		ArgumentNullException.ThrowIfNull(instanceInfo);

		var instanceKey = $"{_keyPrefix}instances:{instanceId}";
		var instanceData = JsonSerializer.Serialize(instanceInfo, RedisJobCoordinatorSerializerContext.Default.JobInstanceInfo);

		_ = await _database.StringSetAsync(instanceKey, instanceData, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
		_ = await _database.SetAddAsync($"{_keyPrefix}instances:active", instanceId).ConfigureAwait(false);

		_logger.LogInformation("Registered job processing instance {InstanceId} on host {HostName}", instanceId, instanceInfo.HostName);
	}

	/// <inheritdoc />
	public async Task UnregisterInstanceAsync(string instanceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

		var instanceKey = $"{_keyPrefix}instances:{instanceId}";

		_ = await _database.KeyDeleteAsync(instanceKey).ConfigureAwait(false);
		_ = await _database.SetRemoveAsync($"{_keyPrefix}instances:active", instanceId).ConfigureAwait(false);

		_logger.LogInformation("Unregistered job processing instance {InstanceId}", instanceId);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<JobInstanceInfo>> GetActiveInstancesAsync(CancellationToken cancellationToken)
	{
		var activeInstanceIds = await _database.SetMembersAsync($"{_keyPrefix}instances:active").ConfigureAwait(false);
		var instances = new List<JobInstanceInfo>();

		foreach (var instanceId in activeInstanceIds)
		{
			var instanceKey = $"{_keyPrefix}instances:{instanceId}";
			var instanceData = await _database.StringGetAsync(instanceKey).ConfigureAwait(false);

			if (instanceData.HasValue)
			{
				try
				{
					var instanceInfo = JsonSerializer.Deserialize(instanceData.ToString(), RedisJobCoordinatorSerializerContext.Default.JobInstanceInfo);
					if (instanceInfo != null)
					{
						instances.Add(instanceInfo);
					}
				}
				catch (JsonException ex)
				{
					_logger.LogWarning(ex, "Failed to deserialize instance info for {InstanceId}", instanceId);
				}
			}
			else
			{
				// Remove stale instance reference
				_ = await _database.SetRemoveAsync($"{_keyPrefix}instances:active", instanceId).ConfigureAwait(false);
			}
		}

		return instances;
	}

	/// <inheritdoc />
	public async Task<string?> DistributeJobAsync(string jobKey, object jobData, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);

		var activeInstances = await GetActiveInstancesAsync(cancellationToken).ConfigureAwait(false);
		var availableInstance = activeInstances
			.Where(static i => i.IsHealthy(TimeSpan.FromMinutes(2)) &&
							   i.ActiveJobCount < i.Capabilities.MaxConcurrentJobs)
			.OrderBy(static i => i.ActiveJobCount)
			.ThenByDescending(static i => i.Capabilities.Priority)
			.FirstOrDefault();

		if (availableInstance != null)
		{
			var jobQueueKey = $"{_keyPrefix}jobs:{availableInstance.InstanceId}";
			var dataElement = JsonSerializer.SerializeToElement(jobData);
			var jobMessage = JsonSerializer.Serialize(
				new RedisJobMessage(jobKey, dataElement),
				RedisJobCoordinatorSerializerContext.Default.RedisJobMessage);

			_ = await _database.ListLeftPushAsync(jobQueueKey, jobMessage).ConfigureAwait(false);

			_logger.LogDebug("Distributed job {JobKey} to instance {InstanceId}", jobKey, availableInstance.InstanceId);
			return availableInstance.InstanceId;
		}

		_logger.LogWarning("No available instances found to process job {JobKey}", jobKey);
		return null;
	}

	/// <inheritdoc />
	public async Task ReportJobCompletionAsync(string jobKey, string instanceId, bool success, object? result,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

		var completionKey = $"{_keyPrefix}completions:{jobKey}";
		JsonElement? resultElement = result is not null ? JsonSerializer.SerializeToElement(result) : null;
		var completionData = JsonSerializer.Serialize(
			new RedisCompletionData(jobKey, instanceId, success, resultElement, DateTimeOffset.UtcNow),
			RedisJobCoordinatorSerializerContext.Default.RedisCompletionData);

		_ = await _database.StringSetAsync(completionKey, completionData, TimeSpan.FromHours(1)).ConfigureAwait(false);

		_logger.LogDebug(
			"Reported completion for job {JobKey} by instance {InstanceId}: {Success}",
			jobKey, instanceId, success ? "Success" : "Failed");
	}
}
