// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// The tenant context of a caller that is deliberately not scoped to any tenant, exposed so that
/// "explicitly untenanted" is something a caller can say rather than something they leave unsaid.
/// </summary>
/// <remarks>
/// <para>
/// Stores that partition rows by tenant resolve that partition from an <see cref="ITenantContext"/>.
/// Where the dependency is optional, a caller cannot distinguish deciding to run untenanted from
/// forgetting to supply a context, and the same store answers the partition question two different
/// ways depending on what happens to be registered in the container. The two answers name different
/// partitions, so rows written under one stop being visible under the other and nothing errors.
/// Naming this instance removes the second answer: the dependency stays required, and a caller who
/// wants no tenant scoping passes this.
/// </para>
/// <para>
/// <strong>Register this only on a deployment that has enabled multi-tenancy.</strong> It names the
/// untenanted partition, which is a live partition only while multi-tenancy is active. On a deployment
/// that has not enabled it, the framework registers its own single-tenant context automatically and a
/// store's startup schema handshake may converge rows anchored to the untenanted term onto the
/// single-tenant identity -- so a context pinned here would go on reading a partition its rows had been
/// migrated out of. Registering it on such a deployment is refused at startup rather than allowed to
/// reach that state; a single-tenant host needs no context of its own and should register none.
/// </para>
/// <para>
/// Absence of a tenant is a VALUE here, not a missing one. <see cref="TenantId"/> is the untenanted
/// partition's own identifier and <see cref="HasTenant"/> is therefore <see langword="true"/> -- a
/// caller in this context is scoped, to the partition that holds untenanted rows. Reporting no tenant
/// would invite a consumer to omit the partition term altogether, which is the defect this type
/// exists to prevent.
/// </para>
/// </remarks>
public sealed class UntenantedContext : ITenantContext
{
	/// <summary>Gets the shared instance.</summary>
	/// <value>The single <see cref="UntenantedContext"/>; it carries no per-caller state.</value>
	public static UntenantedContext Instance { get; } = new();

	private UntenantedContext()
	{
	}

	/// <summary>Gets the untenanted partition's identifier.</summary>
	/// <value>The untenanted sentinel -- never <see langword="null"/> and never empty.</value>
	public string? TenantId => TenantScope.UntenantedSentinel;

	/// <summary>Gets a value indicating whether a partition is resolved.</summary>
	/// <value>Always <see langword="true"/>: the untenanted partition is a partition.</value>
	public bool HasTenant => true;
}
