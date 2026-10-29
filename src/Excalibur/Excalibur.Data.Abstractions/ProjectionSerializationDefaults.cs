// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Data;

/// <summary>
/// Canonical <see cref="JsonSerializerOptions"/> for projection read-model persistence.
/// </summary>
/// <remarks>
/// <para>
/// A projection is a queryable read-model, not an event. This contract is deliberately distinct from the
/// event-payload serializer: it uses camelCase property naming and omits null properties for a compact,
/// consumer-facing document shape, but it <b>preserves the numeric representation of enums</b> (no
/// string-enum converter). Read-model JSON is filtered through SQL / search predicates where an
/// enum-as-string would break range and equality queries, so numeric enums are part of the read-model
/// contract rather than an incidental default.
/// </para>
/// <para>
/// Every projection store sources its options from this single factory so the persisted document shape
/// stays consistent across providers, while still allowing a consumer to supply their own
/// <see cref="JsonSerializerOptions"/> where they need full control.
/// </para>
/// </remarks>
public static class ProjectionSerializationDefaults
{
	/// <summary>
	/// Creates a new mutable <see cref="JsonSerializerOptions"/> instance carrying the projection
	/// read-model contract: camelCase naming, null-property omission, and numeric enum values.
	/// </summary>
	/// <returns>A fresh options instance the caller owns and may further customize.</returns>
	public static JsonSerializerOptions CreateReadModelOptions() => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>
	/// Derives generated read metadata that reads back exactly the document shape <paramref name="writeOptions"/>
	/// produced, using the resolver carried by <paramref name="generatedTypeInfo"/> so no reflection is introduced.
	/// </summary>
	/// <typeparam name="TProjection">The read schema.</typeparam>
	/// <param name="writeOptions">The options the store persists with. These govern the wire contract.</param>
	/// <param name="generatedTypeInfo">Generated metadata supplying the trim-safe resolver for <typeparamref name="TProjection"/>.</param>
	/// <returns>Metadata bound to the persisted document shape.</returns>
	/// <remarks>
	/// The persisted shape is the store's, not the caller's. Generated metadata obtained from a
	/// <see cref="JsonSerializerContext"/> that declares a different naming policy, or omits a converter the
	/// write path applied, describes a different document and would read defaults or wrong values with no error.
	/// Only the resolver is taken from the caller; every option that shapes the wire contract comes from the write path.
	/// </remarks>
	/// <exception cref="NotSupportedException">The supplied metadata carries no resolver, or its resolver does not describe <typeparamref name="TProjection"/>.</exception>
	public static JsonTypeInfo<TProjection> CreateReadModelTypeInfo<TProjection>(
		JsonSerializerOptions writeOptions,
		JsonTypeInfo<TProjection> generatedTypeInfo)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(writeOptions);
		ArgumentNullException.ThrowIfNull(generatedTypeInfo);

		var resolver = generatedTypeInfo.Options.TypeInfoResolver
			?? throw new NotSupportedException(
				"The supplied generated metadata carries no type-info resolver, so metadata matching the persisted document shape cannot be derived. Supply metadata obtained from a JsonSerializerContext.");

		var readOptions = new JsonSerializerOptions(writeOptions) { TypeInfoResolver = resolver };
		return readOptions.TryGetTypeInfo(typeof(TProjection), out var resolved) && resolved is JsonTypeInfo<TProjection> typed
			? typed
			: throw new NotSupportedException(
				$"The supplied generated metadata does not describe '{typeof(TProjection)}'. Declare it on the JsonSerializerContext used for this projection.");
	}
}
