// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Cached value wrapper that preserves type information for proper deserialization.
/// Must be public for System.Text.Json serialization when using distributed caching.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Value"/> property is typed as <c>object?</c> to support polymorphic caching.
/// The <see cref="CachedValueJsonConverter"/> handles serialization by writing the runtime type
/// and deserialization by reading <see cref="TypeName"/> to reconstruct the correct type.
/// This is critical for HybridCache L2 (distributed) serialization round-trips.
/// </para>
/// </remarks>
[JsonConverter(typeof(CachedValueJsonConverter))]
public sealed class CachedValue
{
	/// <summary>
	/// Gets the cached value object.
	/// </summary>
	public object? Value { get; init; }

	/// <summary>
	/// Gets a value indicating whether the value should be cached.
	/// </summary>
	public bool ShouldCache { get; init; }

	/// <summary>
	/// Gets a value indicating whether the handler has been executed.
	/// </summary>
	public bool HasExecuted { get; init; }

	/// <summary>
	/// Gets the assembly-qualified type name for deserialization.
	/// </summary>
	public string? TypeName { get; init; }

	/// <summary>
	/// Gets the identity of the action type that stored this entry, or <see langword="null" /> when it
	/// could not be determined.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A cache key does not commit to the action that produced it. On the <see cref="ICacheable{T}" />
	/// path the key is whatever <c>GetCacheKey()</c> returns, so two different action types can return
	/// the same string and address one entry — <c>$"user:{UserId}"</c> on a name query and an email
	/// query is ordinary code. Where those actions also share a response type, nothing about the stored
	/// value distinguishes them, and the second caller would be served the first's data.
	/// </para>
	/// <para>
	/// Recording the storing action here lets a read attribute the entry and decline one that is not
	/// its own, so the collision costs a cache miss instead of returning another action's value. It is
	/// deliberately not <see cref="System.Type.AssemblyQualifiedName" />: that carries the assembly
	/// version, so a package upgrade would invalidate every stored entry.
	/// </para>
	/// </remarks>
	internal string? ActionTypeName { get; init; }
}
