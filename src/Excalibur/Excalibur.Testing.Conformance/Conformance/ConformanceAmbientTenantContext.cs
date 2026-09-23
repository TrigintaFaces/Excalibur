// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// An <see cref="ITenantContext"/> that resolves the tenant established by
/// <see cref="TenantContextHolder.BeginScope"/>.
/// </summary>
/// <remarks>
/// <para>
/// Inject this into the store you hand to a conformance kit whose tenant-isolation arms vary the
/// ambient scope. Those arms use ONE store and change the ambient tenant around it, because that is
/// the topology production runs: a store is registered as a singleton and resolves the tenant per
/// call. A store handed a FIXED tenant context ignores the ambient scope entirely, so every arm
/// addresses the same partition and the isolation arms pass without exercising isolation.
/// </para>
/// <para>
/// This delegates to <see cref="TenantContextHolder.AmbientContext"/>, which states the same reading
/// rule for every caller. It remains here because a kit deriver names a type, not a property, and
/// because the obligation it carries is a conformance obligation.
/// </para>
/// </remarks>
public sealed class ConformanceAmbientTenantContext : ITenantContext
{
	/// <inheritdoc />
	/// <remarks>
	/// Falls back to the reserved untenanted marker rather than to <see langword="null"/>. A single-tenant
	/// host and an arm running outside any scope both mean "no tenant established", and that state has an
	/// explicit representation in this framework; returning null instead means "no decision was made",
	/// which a store configured to require a tenant refuses outright. Every non-tenant arm in a kit runs
	/// outside a scope, so a null here fails the whole suite rather than the tenancy arms.
	/// </remarks>
	public string? TenantId => TenantContextHolder.AmbientContext.TenantId;

	/// <inheritdoc />
	public bool HasTenant => TenantContextHolder.AmbientContext.HasTenant;
}
