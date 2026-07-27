// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch;

/// <summary>
/// Provides the single canonical <see cref="JsonSerializerOptions"/> contract used to serialize and
/// deserialize event payloads across every event store and the default event serializer.
/// </summary>
/// <remarks>
/// Event stores historically declared their own inline options that diverged from the read path: some
/// omitted the string-enum converter (writing an enum as a number) or the null-ignore condition, so an
/// event written by a store could mis-read when loaded through the serializer. Sourcing one options
/// instance here keeps write and read byte-for-byte compatible on enum and nullable-property handling.
/// </remarks>
public static class EventSerializationDefaults
{
	/// <summary>
	/// Creates a new <see cref="JsonSerializerOptions"/> configured with the canonical event contract:
	/// camelCase property names, enums written as strings, and null values omitted.
	/// </summary>
	/// <remarks>
	/// A fresh instance is returned per call because <see cref="JsonSerializerOptions"/> becomes read-only
	/// once used; callers that need to cache it should hold the returned instance for their own lifetime.
	/// </remarks>
	/// <returns>The canonical serializer options for event payloads and metadata.</returns>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "The non-generic string-enum converter is the reflection-based event serialization path; the AOT opt-in is enforced where the reflection serializer is constructed.")]
	public static JsonSerializerOptions CreateCanonicalOptions() => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() },
	};

	/// <summary>
	/// Gets the single frozen (read-only) canonical <see cref="JsonSerializerOptions"/> instance shared by
	/// every event store and the default event serializer.
	/// </summary>
	/// <remarks>
	/// This instance is <see cref="JsonSerializerOptions.IsReadOnly"/> and therefore safe to share without
	/// defensive copying; it must not be mutated. Callers that need a mutable copy call
	/// <see cref="CreateCanonicalOptions"/>. Prefer reading it through the <c>JsonSerializerOptions.Events</c>
	/// extension accessor at adoption sites.
	/// </remarks>
	/// <value>The frozen canonical serializer options for event payloads and metadata.</value>
	public static JsonSerializerOptions Canonical { get; } = CreateFrozenCanonicalOptions();

	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Canonical is the reflection-based event-serialization contract (matching CreateCanonicalOptions); the AOT opt-in is enforced where the reflection serializer is constructed.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "Canonical is the reflection-based event-serialization contract; the explicit DefaultJsonTypeInfoResolver is the default reflection resolver, consistent with CreateCanonicalOptions.")]
	private static JsonSerializerOptions CreateFrozenCanonicalOptions()
	{
		var options = CreateCanonicalOptions();

		// Canonical is the REFLECTION event serializer for arbitrary consumer event types (unknown at
		// framework build). MakeReadOnly() throws InvalidOperationException on a null TypeInfoResolver, and
		// this runs in the static initializer of Canonical — so freezing without a resolver would fault the
		// whole type (TypeInitializationException) on first access. Attach the default reflection resolver
		// EXPLICITLY (not MakeReadOnly(populateMissingResolver: true), which re-throws at the cctor under
		// reflection-disabled Native AOT). Explicit construction never faults the cctor; it degrades honestly
		// only at an actual serialize of a non-source-generated type under AOT. Native-AOT consumers use the
		// source-generated event context, not this instance.
		options.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
		options.MakeReadOnly();
		return options;
	}
}
