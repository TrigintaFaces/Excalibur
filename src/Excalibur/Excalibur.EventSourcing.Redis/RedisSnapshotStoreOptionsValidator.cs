// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Redis;

/// <summary>Validates <see cref="RedisSnapshotStoreOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class RedisSnapshotStoreOptionsValidator : IValidateOptions<RedisSnapshotStoreOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RedisSnapshotStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(RedisSnapshotStoreOptions.ConnectionString)} must be a non-empty connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.KeyPrefix))
		{
			failures.Add($"{nameof(RedisSnapshotStoreOptions.KeyPrefix)} must be a non-empty key prefix.");
		}

		if (options.SnapshotTtlSeconds < 0)
		{
			failures.Add($"{nameof(RedisSnapshotStoreOptions.SnapshotTtlSeconds)} must not be negative.");
		}

		if (options.DatabaseIndex is < -1 or > 15)
		{
			failures.Add($"{nameof(RedisSnapshotStoreOptions.DatabaseIndex)} must be between -1 and 15.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
