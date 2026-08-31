// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Features;

/// <summary>
/// Extension applying the ambient-tenant fallback to a <see cref="IMessageContext"/> at construction.
/// </summary>
public static class MessageContextTenantFallbackExtensions
{
	/// <summary>
	/// Falls back to <see cref="TenantContextHolder.Current"/> when nothing more specific -- an inbound
	/// header, a message's own metadata, a message's own <c>ITenantAware.TenantId</c>, or causal
	/// propagation from a parent context -- has already supplied a tenant on <paramref name="context"/>.
	/// </summary>
	/// <param name="context">The message context to apply the fallback to.</param>
	/// <remarks>
	/// <para>
	/// This reads <see cref="TenantContextHolder.Current"/> directly rather than resolving
	/// <see cref="ITenantContext"/> through DI. That distinction is load-bearing, not stylistic: on a
	/// deployment that has not opted into multi-tenancy, <see cref="ITenantContext"/> resolves to the
	/// default <c>SingleTenantContext</c>, whose <c>TenantId</c> is a FIXED, non-null constant
	/// (<see cref="TenantDefaults.DefaultTenantId"/>) that ignores the ambient holder entirely -- so a
	/// fallback sourced from <c>ITenantContext</c> cannot express "nothing is established here" on that
	/// registration; it always answers with the default tenant, silently converting a genuinely
	/// untenanted operation into an owned one. <see cref="TenantContextHolder.Current"/> has no such
	/// failure mode: it is <see langword="null"/> exactly when nothing established an ambient tenant for
	/// the current async flow, regardless of which <see cref="ITenantContext"/> implementation (if any)
	/// is registered.
	/// </para>
	/// <para>
	/// <b>Consequence:</b> absence maps to leaving <see cref="IMessageContext"/>'s tenant unset (which
	/// folds to <c>KeyedTenantPartition.Untenanted</c> at the storage boundary) -- never to
	/// <see cref="TenantDefaults.DefaultTenantId"/>. A deployment that has not established an ambient
	/// tenant (including one that runs single-tenant and never calls <c>TenantContextHolder.BeginScope</c>
	/// at all) is not promoted to the default tenant by this fallback; it stays untenanted, exactly as it
	/// did before this fallback existed. This is deliberately narrower than "a single-tenant host gets
	/// <c>__default__</c> on every path" ambition -- doing that safely would require a way to distinguish
	/// "this deployment is single-tenant by design" from "nothing was ever established here," which
	/// <see cref="TenantContextHolder"/> cannot express and <see cref="ITenantContext"/>'s default
	/// registration answers wrongly. That narrower goal is a documented residual gap, not a silent one.
	/// </para>
	/// <para>
	/// Never overwrites a value a more specific source already supplied -- call it only after any
	/// header/metadata/<c>ITenantAware</c>/causal-propagation assignment, never before.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
	public static void ApplyAmbientTenantFallback(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (!string.IsNullOrEmpty(context.GetTenantId()))
		{
			// A more specific source already supplied one -- never overwrite it.
			return;
		}

		// Folded through the framework's single "what counts as untenanted" predicate rather than tested for
		// emptiness here. A drain re-establishes a stored row's tenant as ambient, and an untenanted row's
		// term is the reserved sentinel -- a concrete, non-empty string. Read raw, that string is stamped
		// onto the context and travels onto the rows this operation writes, where a deliberate absence was
		// written before. ToSignedTenantId maps every spelling of untenanted (null, empty, whitespace, the
		// sentinel) back to null, so absence stays absence no matter which spelling the ambient carries.
		var ambientTenantId = KeyedTenantPartition.ToSignedTenantId(TenantContextHolder.Current);
		if (!string.IsNullOrEmpty(ambientTenantId))
		{
			context.GetOrCreateIdentityFeature().TenantId = ambientTenantId;
		}

		// else: absence stays absence. Leaving IdentityFeature.TenantId unset folds to
		// KeyedTenantPartition.Untenanted at the storage boundary, never TenantDefaults.DefaultTenantId.
	}
}
