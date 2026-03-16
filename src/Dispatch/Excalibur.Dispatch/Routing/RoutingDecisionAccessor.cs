// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Internal fast-path accessor for <see cref="RoutingDecision"/> that bypasses Features dictionary
/// lookups when the context is a <see cref="MessageContext"/>.
/// Follows the <c>HttpContext</c> pattern of caching frequently-accessed features as direct fields.
/// </summary>
/// <remarks>
/// For <see cref="MessageContext"/>, the <see cref="MessageContext.CachedRoutingDecision"/> field
/// is the single source of truth. The Features dictionary is NOT written on set — this eliminates
/// the ~80B allocation for <c>MessageRoutingFeature</c> + dictionary entry on the hot path.
/// Non-MessageContext implementations still use the Features dictionary as before.
/// </remarks>
internal static class RoutingDecisionAccessor
{
	/// <summary>
	/// Gets the routing decision using the cached field when available, falling back to
	/// the Features dictionary for non-MessageContext implementations.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static RoutingDecision? GetRoutingDecisionFast(IMessageContext context)
	{
		if (context is MessageContext mc)
		{
			// Fast path: check cached field first, fall back to Features dictionary
			// so routing decisions set via the public API (GetOrCreateRoutingFeature) are still visible.
			return mc.CachedRoutingDecision ?? context.GetRoutingFeature()?.RoutingDecision;
		}

		return context.GetRoutingFeature()?.RoutingDecision;
	}

	/// <summary>
	/// Sets the routing decision on the cached field (when MessageContext) or
	/// the Features dictionary (for other implementations).
	/// </summary>
	/// <remarks>
	/// For MessageContext, only the direct field is written — no Features dictionary allocation.
	/// This saves ~80B per routing operation (MessageRoutingFeature object + dictionary entry).
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void SetRoutingDecision(IMessageContext context, RoutingDecision decision)
	{
		if (context is MessageContext mc)
		{
			mc.CachedRoutingDecision = decision;
			return;
		}

		context.GetOrCreateRoutingFeature().RoutingDecision = decision;
	}
}
