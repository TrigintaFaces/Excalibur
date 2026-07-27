// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Redis;

/// <summary>Validates <see cref="RedisEventStoreOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class RedisEventStoreOptionsValidator : IValidateOptions<RedisEventStoreOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RedisEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(RedisEventStoreOptions.ConnectionString)} must be a non-empty connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.StreamKeyPrefix))
		{
			failures.Add($"{nameof(RedisEventStoreOptions.StreamKeyPrefix)} must be a non-empty key prefix.");
		}

		if (options.DatabaseIndex is < -1 or > 15)
		{
			failures.Add($"{nameof(RedisEventStoreOptions.DatabaseIndex)} must be between -1 and 15.");
		}

		if (options.DefaultBatchSize < 1)
		{
			failures.Add($"{nameof(RedisEventStoreOptions.DefaultBatchSize)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
