// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Data stored in Redis when acquiring a distributed lock.
/// </summary>
/// <param name="InstanceId">The instance that acquired the lock.</param>
/// <param name="AcquiredAt">When the lock was acquired.</param>
/// <param name="ExpiresAt">When the lock expires.</param>
internal sealed record RedisLockData(string InstanceId, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt);

/// <summary>
/// Data stored in Redis when distributing a job to an instance.
/// </summary>
/// <param name="JobKey">The job key.</param>
/// <param name="Data">The serialized job data.</param>
internal sealed record RedisJobMessage(string JobKey, JsonElement Data);

/// <summary>
/// Data stored in Redis when reporting job completion.
/// </summary>
/// <param name="JobKey">The job key.</param>
/// <param name="InstanceId">The instance that completed the job.</param>
/// <param name="Success">Whether the job succeeded.</param>
/// <param name="Result">The serialized result, if any.</param>
/// <param name="CompletedAt">When the job completed.</param>
internal sealed record RedisCompletionData(string JobKey, string InstanceId, bool Success, JsonElement? Result, DateTimeOffset CompletedAt);

/// <summary>
/// Source-generated JSON serializer context for Redis job coordinator data types.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false)]
[JsonSerializable(typeof(RedisLockData))]
[JsonSerializable(typeof(RedisJobMessage))]
[JsonSerializable(typeof(RedisCompletionData))]
[JsonSerializable(typeof(JobInstanceInfo))]
internal sealed partial class RedisJobCoordinatorSerializerContext : JsonSerializerContext;
