// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Validates <see cref="ShardMapOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ShardMapOptionsValidator : IValidateOptions<ShardMapOptions>
{
	private readonly ITenantShardMap? _shardMap;

	internal ShardMapOptionsValidator(ITenantShardMap? shardMap = null)
	{
		_shardMap = shardMap;
	}

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ShardMapOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.EnableTenantSharding && _shardMap is not null)
		{
			var shardIds = _shardMap.GetRegisteredShardIds();
			if (shardIds.Count == 0)
			{
				failures.Add("At least one shard must be registered when EnableTenantSharding is true.");
			}

			if (options.DefaultShardId is { } defaultShardId && !shardIds.Contains(defaultShardId))
			{
				failures.Add($"{nameof(ShardMapOptions.DefaultShardId)} '{defaultShardId}' does not reference a registered shard. Registered shards: {string.Join(", ", shardIds)}.");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
