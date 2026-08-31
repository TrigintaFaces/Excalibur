// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Writes event payloads and metadata under the canonical event wire contract, resolving type metadata
/// through the host's source-generated resolver when one was configured and through reflection when not.
/// </summary>
/// <remarks>
/// A collaborator rather than methods on the store: the reflection-free path needs the low-level writer
/// types, and folding those into the store pushed its class coupling past the analyzer's ceiling. The
/// separation also keeps the wire contract in one readable place.
/// </remarks>
internal sealed class CosmosDbEventPayloadWriter
{
	private readonly JsonSerializerOptions _jsonOptions =
		EventSerializationDefaults.CreateCanonicalOptions();

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free path. Decided
	/// once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventPayloadWriter"/> class.
	/// </summary>
	/// <param name="eventTypeInfoResolver">
	/// The host's source-generated resolver, or <see langword="null"/> to serialize through reflection.
	/// </param>
	/// <remarks>
	/// The resolver supplies type METADATA only. It is attached to the canonical options rather than
	/// replacing them, so the naming policy, string-enum representation and null handling that fix the
	/// stored wire format stay the store's own and apply to whichever resolver is in use -- events written
	/// with a resolver are byte-identical to events written without one.
	/// </remarks>
	public CosmosDbEventPayloadWriter(IJsonTypeInfoResolver? eventTypeInfoResolver)
	{
		_hasEventTypeInfoResolver =
			EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, eventTypeInfoResolver);
	}

	/// <summary>
	/// Serializes a domain event, resolving its type metadata from the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <param name="evt">The domain event to serialize.</param>
	/// <param name="aggregateId">The stream the append targets, reported if the type is undeclared.</param>
	/// <param name="aggregateType">The aggregate type the append targets, reported if undeclared.</param>
	/// <returns>The UTF-8 encoded event payload.</returns>
	/// <exception cref="Excalibur.EventSourcing.EventTypeNotDeclaredException">
	/// The configured resolver does not declare the event's runtime type.
	/// </exception>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	public byte[] SerializeEvent(IDomainEvent evt, string? aggregateId, string? aggregateType)
	{
		ArgumentNullException.ThrowIfNull(evt);

		return _hasEventTypeInfoResolver
			? Excalibur.EventSourcing.ResolvedEventPayload.Serialize(evt, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), _jsonOptions);
	}

	/// <summary>
	/// Serializes event metadata, dispatching each value through the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <param name="metadata">The event metadata to serialize.</param>
	/// <returns>The UTF-8 encoded metadata object.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	public byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		_hasEventTypeInfoResolver
			? EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
			: JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
}
