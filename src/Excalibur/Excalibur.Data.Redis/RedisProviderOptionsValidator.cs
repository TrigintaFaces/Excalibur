// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Validates <see cref="RedisProviderOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Performs cross-property constraint checks beyond what <see cref="System.ComponentModel.DataAnnotations"/> can express.
/// </summary>
internal sealed class RedisProviderOptionsValidator : IValidateOptions<RedisProviderOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RedisProviderOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(RedisProviderOptions.ConnectionString)} must not be empty.");
		}

		if (options.DatabaseId < 0)
		{
			failures.Add($"{nameof(RedisProviderOptions.DatabaseId)} must be >= 0 (was {options.DatabaseId}).");
		}

		if (options.Pool.ConnectTimeout < 1)
		{
			failures.Add(
				$"{nameof(RedisConnectionPoolOptions)}.{nameof(RedisConnectionPoolOptions.ConnectTimeout)} " +
				$"must be >= 1 (was {options.Pool.ConnectTimeout}).");
		}

		if (options.Pool.SyncTimeout < 1)
		{
			failures.Add(
				$"{nameof(RedisConnectionPoolOptions)}.{nameof(RedisConnectionPoolOptions.SyncTimeout)} " +
				$"must be >= 1 (was {options.Pool.SyncTimeout}).");
		}

		if (options.Pool.AsyncTimeout < 1)
		{
			failures.Add(
				$"{nameof(RedisConnectionPoolOptions)}.{nameof(RedisConnectionPoolOptions.AsyncTimeout)} " +
				$"must be >= 1 (was {options.Pool.AsyncTimeout}).");
		}

		if (options.Pool.ConnectRetry < 0)
		{
			failures.Add(
				$"{nameof(RedisConnectionPoolOptions)}.{nameof(RedisConnectionPoolOptions.ConnectRetry)} " +
				$"must be >= 0 (was {options.Pool.ConnectRetry}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
