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
}
