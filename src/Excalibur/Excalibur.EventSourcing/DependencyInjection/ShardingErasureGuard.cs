// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Options marker whose <c>ValidateOnStart</c> registration triggers the sharding + erasure fail-closed gate
/// at host startup. Carries no configuration; it exists only to hang a startup validation off.
/// </summary>
internal sealed class ShardingErasureGuardOptions;

/// <summary>
/// Fails fast at host startup when GDPR event-store erasure is composed together with tenant-sharding, which
/// is not yet supported: the tenant-routing event store does not route erasure to per-tenant shards, so an
/// erase could not be applied to the subject's shard. Making the unsupported combination unreachable at boot
/// (rather than throwing later, or — worse — appearing to succeed) keeps the erasure contract honest.
/// </summary>
/// <remarks>
/// The gate keys on the presence of the tenant-sharding marker, registered by the routing wiring for both
/// sharding entry points (the <c>EnableTenantSharding</c> builder seam and <c>AddMultiTenancy</c>'s sharding
/// strategy). A non-sharding host never registers the marker and validates successfully, so ordinary
/// event-store erasure is unaffected.
/// </remarks>
internal sealed class ShardingErasureGuard(IServiceProvider serviceProvider)
	: IValidateOptions<ShardingErasureGuardOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ShardingErasureGuardOptions options)
		=> serviceProvider.GetService<Sharding.TenantShardingActiveMarker>() is not null
			? ValidateOptionsResult.Fail(
				"GDPR event-store erasure is not supported together with tenant-sharding (EnableTenantSharding). "
				+ "The tenant-routing event store does not route erasure to per-tenant shards, so an erase could "
				+ "not be applied to the subject's shard. Remove EnableTenantSharding, or do not enable "
				+ "event-store erasure, until sharded erasure routing is available.")
			: ValidateOptionsResult.Success;
}
