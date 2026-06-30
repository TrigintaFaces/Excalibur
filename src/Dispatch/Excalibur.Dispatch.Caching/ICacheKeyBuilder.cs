// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Creates cache keys for dispatch requests.
/// </summary>
public interface ICacheKeyBuilder
{
	/// <summary>
	/// Builds a stable, hash-safe cache key for the given message and context, or <see langword="null" /> when no
	/// cache identity can be derived for the action.
	/// </summary>
	/// <param name="action">The dispatch action to create a key for.</param>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// A stable cache key string, or <see langword="null" /> when no cache identity can be derived for
	/// <paramref name="action" /> — for example the action declares <c>ICacheable&lt;T&gt;</c> but its key cannot be
	/// resolved, or the action cannot be serialized. A <see langword="null" /> result means "do not cache": the
	/// caller MUST bypass the cache and invoke the underlying operation. Implementations MUST be infallible for a
	/// "cannot derive a key" condition — return <see langword="null" /> rather than throwing.
	/// </returns>
	[RequiresDynamicCode("JSON serialization requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	string? CreateKey(IDispatchAction action, IMessageContext context);

	/// <summary>
	/// Builds the stored cache key for a logical key under a tenant/user identity, applying the same identity
	/// folding and hashing transform as <see cref="CreateKey(IDispatchAction, IMessageContext)"/> — without a live
	/// <see cref="IMessageContext"/>.
	/// </summary>
	/// <param name="logicalKey">The logical cache identity (e.g. the value a cacheable query exposes via <c>GetCacheKey()</c>).</param>
	/// <param name="tenantId">The tenant identity the entry was stored under, or <see langword="null"/> for the global tenant.</param>
	/// <param name="userId">The user identity the entry was stored under, or <see langword="null"/> for an anonymous user.</param>
	/// <returns>
	/// The storage key that a cacheable query would have stored for <paramref name="logicalKey"/> under the given
	/// identity. Use this to turn a direct invalidation key into the exact key to remove — a raw logical key can
	/// never equal the stored hashed key.
	/// </returns>
	/// <remarks>
	/// This overload performs no serialization and is AOT- and trimming-safe. It is intended for direct-key cache
	/// invalidation, where the tenant/user are derived from the invalidating dispatch's context rather than the
	/// original cached request.
	/// </remarks>
	string CreateKey(string logicalKey, string? tenantId, string? userId);
}
