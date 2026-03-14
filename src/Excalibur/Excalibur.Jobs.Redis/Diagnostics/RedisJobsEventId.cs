// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Jobs.Redis.Diagnostics;

/// <summary>
/// Event IDs for Redis job coordination logging.
/// Range: 147400-147409 (allocated from Excalibur.Jobs event ID space).
/// </summary>
internal static class RedisJobsEventId
{
	public const int RedisLockAcquired = 147400;
	public const int RedisLockAcquisitionFailed = 147401;
	public const int RedisInstanceRegistered = 147402;
	public const int RedisInstanceUnregistered = 147403;
	public const int RedisInstanceDeserializationFailed = 147404;
	public const int RedisLeaderElected = 147405;
	public const int RedisLeaderDeserializationFailed = 147406;
	public const int RedisJobDistributed = 147407;
	public const int RedisNoInstanceAvailable = 147408;
	public const int RedisJobCompletionReported = 147409;
}
