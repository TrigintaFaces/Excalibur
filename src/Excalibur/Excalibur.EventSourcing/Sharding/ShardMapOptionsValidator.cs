// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Validates <see cref="ShardMapOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Public so the ASP.NET Core options validation infrastructure can activate the
/// validator via DI when tenant sharding is enabled and the consumer registers
/// only the ShardMapOptions (no <see cref="ITenantShardMap"/>). The optional
/// <see cref="ITenantShardMap"/> dependency keeps the validator usable in
/// single-tenant or deferred-registration scenarios.
/// </remarks>
public sealed class ShardMapOptionsValidator : IValidateOptions<ShardMapOptions>
{
	private readonly ITenantShardMap? _shardMap;

	/// <summary>
	/// Initializes a new instance with an optional <see cref="ITenantShardMap"/> dependency.
	/// </summary>
	/// <param name="shardMap">The tenant shard map, if registered.</param>
	public ShardMapOptionsValidator(ITenantShardMap? shardMap = null)
	{
		_shardMap = shardMap;
	}

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ShardMapOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// Validator is only registered when EnableTenantSharding(...) is invoked, so
		// presence of the validator itself signals sharding is enabled. [bd-51k0mc]
		if (_shardMap is not null)
		{
			var shardIds = _shardMap.GetRegisteredShardIds();
			if (shardIds.Count == 0)
			{
				failures.Add("At least one shard must be registered when tenant sharding is enabled.");
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
