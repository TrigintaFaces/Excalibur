// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Redis.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Redis.Coordination;

internal static partial class RedisJobCoordinatorLog
{
	[LoggerMessage(RedisJobsEventId.RedisLockAcquired, LogLevel.Debug, "Acquired distributed lock for job {JobKey} by instance {InstanceId}")]
	public static partial void LockAcquired(ILogger logger, string jobKey, string instanceId);

	[LoggerMessage(RedisJobsEventId.RedisLockAcquisitionFailed, LogLevel.Debug, "Failed to acquire distributed lock for job {JobKey} by instance {InstanceId}")]
	public static partial void LockAcquisitionFailed(ILogger logger, string jobKey, string instanceId);

	[LoggerMessage(RedisJobsEventId.RedisInstanceRegistered, LogLevel.Information, "Registered job processing instance {InstanceId} on host {HostName}")]
	public static partial void InstanceRegistered(ILogger logger, string instanceId, string hostName);

	[LoggerMessage(RedisJobsEventId.RedisInstanceUnregistered, LogLevel.Information, "Unregistered job processing instance {InstanceId}")]
	public static partial void InstanceUnregistered(ILogger logger, string instanceId);

	[LoggerMessage(RedisJobsEventId.RedisInstanceDeserializationFailed, LogLevel.Warning, "Failed to deserialize instance info for {InstanceId}")]
	public static partial void InstanceDeserializationFailed(ILogger logger, Exception exception, string instanceId);

	[LoggerMessage(RedisJobsEventId.RedisLeaderElected, LogLevel.Information, "Instance {InstanceId} elected as leader")]
	public static partial void LeaderElected(ILogger logger, string instanceId);

	[LoggerMessage(RedisJobsEventId.RedisLeaderDeserializationFailed, LogLevel.Warning, "Failed to deserialize leader info")]
	public static partial void LeaderDeserializationFailed(ILogger logger, Exception exception);

	[LoggerMessage(RedisJobsEventId.RedisJobDistributed, LogLevel.Debug, "Distributed job {JobKey} to instance {InstanceId}")]
	public static partial void JobDistributed(ILogger logger, string jobKey, string instanceId);

	[LoggerMessage(RedisJobsEventId.RedisNoInstanceAvailable, LogLevel.Warning, "No available instances found to process job {JobKey}")]
	public static partial void NoInstanceAvailable(ILogger logger, string jobKey);

	[LoggerMessage(RedisJobsEventId.RedisJobCompletionReported, LogLevel.Debug, "Reported completion for job {JobKey} by instance {InstanceId}: {Status}")]
	public static partial void JobCompletionReported(ILogger logger, string jobKey, string instanceId, string status);
}
