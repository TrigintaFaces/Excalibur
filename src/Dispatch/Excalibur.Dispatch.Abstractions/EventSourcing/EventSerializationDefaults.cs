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


	/// <summary>
	/// Attaches a host-supplied source-generated type-info resolver to canonical event serializer options
	/// and freezes them.
	/// </summary>
	/// <remarks>
	/// The resolver supplies type METADATA only. It is attached to the canonical options rather than
	/// replacing them, so the naming policy, string-enum representation and null handling that fix the
	/// stored wire format stay canonical and apply to whichever resolver is in use -- events written with a
	/// resolver are byte-identical to events written without one.
	/// </remarks>
	/// <param name="jsonOptions">The canonical serializer options to attach the resolver to.</param>
	/// <param name="resolver">The host's resolver, or <see langword="null"/> to keep the reflection path.</param>
	/// <returns><see langword="true"/> when a resolver was attached.</returns>
	public static bool TryApplyTypeInfoResolver(
		JsonSerializerOptions jsonOptions,
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? resolver)
	{
		ArgumentNullException.ThrowIfNull(jsonOptions);

		if (resolver is null)
		{
			return false;
		}

		jsonOptions.TypeInfoResolver = resolver;
		jsonOptions.MakeReadOnly();
		return true;
	}

	/// <summary>
	/// Writes event metadata a property at a time, resolving each value against its own runtime type through
	/// the resolver carried by the supplied options.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Metadata is an <c>IDictionary&lt;string, object&gt;</c>, and the reflection serializer writes each
	/// value as its runtime type. Source generation resolves nothing at run time, so the dictionary cannot
	/// simply be handed to the resolver: the honest form of "each value as its runtime type" is to write the
	/// object here and look up <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo"/> per value.
	/// Declaring <c>Dictionary&lt;string, object&gt;</c> on a context instead would compile and then throw on
	/// the very values it appeared to cover.
	/// </para>
	/// <para>
	/// The output is byte-identical to the reflection path. Keys are written verbatim (no dictionary key
	/// policy is configured), the writer inherits the encoder and indentation from the same options, and a
	/// null value is written as an explicit <c>null</c> -- <see cref="JsonIgnoreCondition.WhenWritingNull"/>
	/// governs object properties, not dictionary entries, so the reflection path emits it too.
	/// </para>
	/// <para>
	/// Call this only when <see cref="TryApplyTypeInfoResolver"/> reported that a resolver was attached; with
	/// no resolver the reflection path (<c>JsonSerializer.SerializeToUtf8Bytes(metadata, jsonOptions)</c>) is
	/// the equivalent and cheaper form.
	/// </para>
	/// </remarks>
	/// <param name="metadata">The event metadata to write.</param>
	/// <param name="jsonOptions">The canonical serializer options carrying the host's resolver.</param>
	/// <returns>The UTF-8 encoded metadata object.</returns>
	public static byte[] SerializeMetadataWithResolver(
		IDictionary<string, object> metadata,
		JsonSerializerOptions jsonOptions)
	{
		ArgumentNullException.ThrowIfNull(metadata);
		ArgumentNullException.ThrowIfNull(jsonOptions);

		var buffer = new System.Buffers.ArrayBufferWriter<byte>();

		using (var writer = new Utf8JsonWriter(
			buffer,
			new JsonWriterOptions { Encoder = jsonOptions.Encoder, Indented = jsonOptions.WriteIndented }))
		{
			writer.WriteStartObject();

			foreach (var entry in metadata)
			{
				writer.WritePropertyName(entry.Key);

				if (entry.Value is null)
				{
					writer.WriteNullValue();
					continue;
				}

				JsonSerializer.Serialize(writer, entry.Value, jsonOptions.GetTypeInfo(entry.Value.GetType()));
			}

			writer.WriteEndObject();
		}

		return buffer.WrittenSpan.ToArray();
	}

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
