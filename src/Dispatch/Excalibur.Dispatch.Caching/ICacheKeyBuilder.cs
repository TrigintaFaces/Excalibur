// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Creates cache keys for dispatch requests.
/// </summary>
public interface ICacheKeyBuilder
{
	/// <summary>
	/// Builds a stable, hash-safe cache key for the given message and context.
	/// </summary>
	/// <param name="action">The dispatch action to create a key for.</param>
	/// <param name="context">The message context.</param>
	/// <returns>A stable cache key string.</returns>
	[RequiresDynamicCode("JSON serialization requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	string CreateKey(IDispatchAction action, IMessageContext context);
}
