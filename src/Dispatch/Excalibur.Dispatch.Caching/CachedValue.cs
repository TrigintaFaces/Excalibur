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
}
