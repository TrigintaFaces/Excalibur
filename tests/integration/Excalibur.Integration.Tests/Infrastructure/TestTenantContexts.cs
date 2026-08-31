// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.Infrastructure;

/// <summary>
/// An <see cref="ITenantContext"/> pinned to one identifier, for suites that need a store to
/// resolve a specific tenant partition without standing up a resolver.
/// </summary>
internal sealed class FixedTestTenantContext(string? tenantId) : ITenantContext
{
	/// <inheritdoc />
	public string? TenantId { get; } = tenantId;

	/// <inheritdoc />
	public bool HasTenant => !string.IsNullOrEmpty(TenantId);
}

/// <summary>
/// The context a single-tenant host receives: the one canonical tenant identity
/// (<see cref="TenantDefaults.DefaultTenantId"/>).
/// </summary>
/// <remarks>
/// This is what a suite that never distinguished tenants is asserting against. Such a suite used
/// to pass no context at all, which the stores read as "partition undecided"; that state no longer
/// exists, and the deployment those suites describe — one host, one tenant — operates as this
/// identity. It is a real tenant identity, not a sentinel: rows written under it are readable under
/// it, which is what these suites round-trip.
/// </remarks>
internal sealed class SingleTenantTestContext : ITenantContext
{
	/// <summary>Gets the shared instance. The type is immutable, so one instance serves every suite.</summary>
	public static readonly SingleTenantTestContext Instance = new();

	/// <inheritdoc />
	public string? TenantId => TenantDefaults.DefaultTenantId;

	/// <inheritdoc />
	public bool HasTenant => true;
}

/// <summary>
/// An <see cref="ITenantContext"/> that reads the ambient tenant established by
/// <c>TenantContextHolder.BeginScope</c>, for suites that switch tenant per operation.
/// </summary>
internal sealed class AmbientHolderTestTenantContext : ITenantContext
{
	/// <inheritdoc />
	public string? TenantId => TenantContextHolder.Current;

	/// <inheritdoc />
	public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
}

/// <summary>
/// The context for a store operating on the <b>untenanted partition</b>: it resolves
/// <see cref="TenantScope.UntenantedSentinel"/>, which <c>TenantScope.FromContext</c> maps to
/// <see cref="TenantScope.Untenanted"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the exact term a keyed store used to bind when it was handed no context at all: an absent
/// context became <c>TenantScope.Untenanted</c>, and every keyed statement routed that through
/// <c>KeyedTenantPartition.FromScope</c>, which reinterprets <c>None</c> as the reserved untenanted
/// partition. Suites whose seeded rows, migrations, or regression guards are about that partition need
/// this context and not the default tenant identity — under
/// <see cref="TenantDefaults.DefaultTenantId"/> they would bind an ordinary tenant term, stop
/// exercising the untenanted branch, and pass while proving nothing about it.
/// </para>
/// <para>
/// The sentinel is a storage encoding rather than a tenant name, and the conversion is total for it, so
/// resolving it here is a supported input and not a smuggled real tenant.
/// </para>
/// </remarks>
internal sealed class UntenantedTestTenantContext : ITenantContext
{
	/// <summary>Gets the shared instance. The type is immutable, so one instance serves every suite.</summary>
	public static readonly UntenantedTestTenantContext Instance = new();

	/// <inheritdoc />
	public string? TenantId => TenantScope.UntenantedSentinel;

	/// <inheritdoc />
	public bool HasTenant => true;
}
