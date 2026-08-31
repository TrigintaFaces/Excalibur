// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Declares that a store implementation persists the tenant discriminator on every row it writes and
/// re-establishes the owning tenant from the row on read, rather than reading it from an injected
/// ambient <see cref="ITenantContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddTenantAwareStore</c> derives the ambient-scoped mechanism structurally, from whether a store's
/// constructor declares an <see cref="ITenantContext"/> parameter — a fact the type system can prove.
/// The row-partitioned mechanism has no equivalent structural signal: a constructor with no
/// <see cref="ITenantContext"/> parameter is consistent with a store that carries the tenant on the row
/// <em>and</em> with a store that implements no tenancy mechanism at all. The absence of one claim is
/// not evidence for the other, so the seam does not infer row-partitioning from what a constructor
/// omits — it requires a store to state the claim itself, by implementing this interface.
/// </para>
/// <para>
/// A store that implements neither this interface nor takes an <see cref="ITenantContext"/> constructor
/// parameter is registered with no tenancy capability marker at all — the same outcome as a store built
/// before either mechanism existed. Row-discriminator multi-tenancy then fails closed if the contract it
/// registers under requires a capability marker (<c>RequireTenantScopingCapability&lt;TContract&gt;</c> /
/// <c>RequireTenantPartitionedCapability&lt;TContract&gt;</c>), exactly as it does for any other
/// unattested provider.
/// </para>
/// </remarks>
public interface ITenantPartitionedStore
{
}
