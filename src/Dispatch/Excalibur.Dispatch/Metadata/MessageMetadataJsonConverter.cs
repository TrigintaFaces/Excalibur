// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Serializes <see cref="MessageMetadata"/> using a flat JSON object shape, preserving the wire
/// contract that existed before the metadata fields were composed into focused value-type groups
/// (<see cref="MessageIdentity"/>, <see cref="MessageRouting"/>, etc.).
/// </summary>
/// <remarks>
/// Composing the metadata into nested groups is a C# API concern only. This converter hoists the
/// grouped fields back to the top level on write and routes flat fields back into the builder on
/// read, so the serialized representation is unchanged for consumers and transports. The converter
/// is hand-written (no reflection) and therefore AOT/trimming safe.
/// </remarks>
public sealed class MessageMetadataJsonConverter : JsonConverter<MessageMetadata>
{
	/// <inheritdoc />
	public override MessageMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException("Expected start of object for MessageMetadata.");
		}

		var builder = new MessageMetadataBuilder();

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				return (MessageMetadata)builder.Build();
			}

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException("Expected property name in MessageMetadata object.");
			}

			var propertyName = reader.GetString() ?? string.Empty;
			_ = reader.Read();

			ApplyProperty(builder, propertyName, ref reader, options);
		}

		throw new JsonException("Unexpected end of JSON while reading MessageMetadata.");
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, MessageMetadata value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStartObject();

		// Core identity (root)
		WriteString(writer, options, "messageId", value.MessageId);
		WriteString(writer, options, "correlationId", value.CorrelationId);
		WriteOptionalString(writer, options, "causationId", value.CausationId);
		WriteString(writer, options, "messageType", value.MessageType);
		WriteString(writer, options, "contentType", value.ContentType);
		WriteOptionalString(writer, options, "source", value.Source);
		WritePropertyName(writer, options, "createdTimestampUtc");
		writer.WriteStringValue(value.CreatedTimestampUtc);

		// Identity group
		WriteOptionalString(writer, options, "externalId", value.Identity.ExternalId);
		WriteOptionalString(writer, options, "contentEncoding", value.Identity.ContentEncoding);
		WriteOptionalString(writer, options, "messageVersion", value.Identity.MessageVersion);
		WriteOptionalString(writer, options, "serializerVersion", value.Identity.SerializerVersion);
		WriteOptionalString(writer, options, "contractVersion", value.Identity.ContractVersion);

		// Routing group
		WriteOptionalString(writer, options, "destination", value.Routing.Destination);
		WriteOptionalString(writer, options, "replyTo", value.Routing.ReplyTo);
		WriteOptionalString(writer, options, "sessionId", value.Routing.SessionId);
		WriteOptionalString(writer, options, "partitionKey", value.Routing.PartitionKey);
		WriteOptionalString(writer, options, "routingKey", value.Routing.RoutingKey);
		WriteOptionalString(writer, options, "groupId", value.Routing.GroupId);
		WriteOptionalLong(writer, options, "groupSequence", value.Routing.GroupSequence);

		// Timing group
		WriteOptionalDate(writer, options, "sentTimestampUtc", value.Timing.SentTimestampUtc);
		WriteOptionalDate(writer, options, "receivedTimestampUtc", value.Timing.ReceivedTimestampUtc);
		WriteOptionalDate(writer, options, "scheduledEnqueueTimeUtc", value.Timing.ScheduledEnqueueTimeUtc);
		WriteOptionalTimeSpan(writer, options, "timeToLive", value.Timing.TimeToLive);
		WriteOptionalDate(writer, options, "expiresAtUtc", value.Timing.ExpiresAtUtc);

		// Observability group
		WriteOptionalString(writer, options, "traceParent", value.Observability.TraceParent);
		WriteOptionalString(writer, options, "traceState", value.Observability.TraceState);
		WriteOptionalString(writer, options, "baggage", value.Observability.Baggage);

		// Delivery group
		WritePropertyName(writer, options, "deliveryCount");
		writer.WriteNumberValue(value.Delivery.DeliveryCount);
		WriteOptionalInt(writer, options, "maxDeliveryCount", value.Delivery.MaxDeliveryCount);
		WriteOptionalString(writer, options, "lastDeliveryError", value.Delivery.LastDeliveryError);
		WriteOptionalString(writer, options, "deadLetterQueue", value.Delivery.DeadLetterQueue);
		WriteOptionalString(writer, options, "deadLetterReason", value.Delivery.DeadLetterReason);
		WriteOptionalString(writer, options, "deadLetterErrorDescription", value.Delivery.DeadLetterErrorDescription);
		WriteOptionalInt(writer, options, "priority", value.Delivery.Priority);
		WriteOptionalBool(writer, options, "durable", value.Delivery.Durable);
		WriteOptionalBool(writer, options, "requiresDuplicateDetection", value.Delivery.RequiresDuplicateDetection);
		WriteOptionalTimeSpan(writer, options, "duplicateDetectionWindow", value.Delivery.DuplicateDetectionWindow);

		// Event sourcing group
		WriteOptionalString(writer, options, "aggregateId", value.EventSourcing.AggregateId);
		WriteOptionalString(writer, options, "aggregateType", value.EventSourcing.AggregateType);
		WriteOptionalLong(writer, options, "aggregateVersion", value.EventSourcing.AggregateVersion);
		WriteOptionalString(writer, options, "streamName", value.EventSourcing.StreamName);
		WriteOptionalLong(writer, options, "streamPosition", value.EventSourcing.StreamPosition);
		WriteOptionalLong(writer, options, "globalPosition", value.EventSourcing.GlobalPosition);
		WriteOptionalString(writer, options, "eventType", value.EventSourcing.EventType);
		WriteOptionalInt(writer, options, "eventVersion", value.EventSourcing.EventVersion);

		// Security group
		WriteOptionalString(writer, options, "userId", value.Security.UserId);
		WriteOptionalString(writer, options, "tenantId", value.Security.TenantId);
		if (value.Security.Roles is { Count: > 0 } roles)
		{
			WritePropertyName(writer, options, "roles");
			writer.WriteStartArray();
			foreach (var role in roles)
			{
				writer.WriteStringValue(role);
			}

			writer.WriteEndArray();
		}

		// Extensibility bags
		if (value.Headers is { Count: > 0 } headers)
		{
			WritePropertyName(writer, options, "headers");
			writer.WriteStartObject();
			foreach (var header in headers)
			{
				writer.WritePropertyName(header.Key);
				writer.WriteStringValue(header.Value);
			}

			writer.WriteEndObject();
		}

		// Typed Attributes/Items bags (az9u1e): the builder stores these in the Properties bag under
		// the well-known keys, surfaced by GetAttributes()/GetItems(). Emit them as top-level objects
		// so they round-trip losslessly (the generic properties loop below skips these keys to avoid a
		// stringified-dictionary duplicate). SA wire-contract ruling 16887 (FR-C2 / EC-6).
		WriteObjectBag(writer, options, "attributes", value.Properties, MetadataPropertyKeys.Attributes);
		WriteObjectBag(writer, options, "items", value.Properties, MetadataPropertyKeys.Items);

		// Security claims (az9u1e): explicit lossless array of {type,value,valueType?,issuer?}. Optional
		// fields are omitted when they equal the BCL defaults so empty/default claims stay compact;
		// Read reconstructs them with the same defaults. No silent drop (the pre-fix converter dropped
		// claims entirely). SA ruling 16887.
		if (value.Security.Claims is { Count: > 0 } claims)
		{
			WritePropertyName(writer, options, "claims");
			writer.WriteStartArray();
			foreach (var claim in claims)
			{
				writer.WriteStartObject();
				writer.WriteString("type", claim.Type);
				writer.WriteString("value", claim.Value);
				if (!string.IsNullOrEmpty(claim.ValueType) && !string.Equals(claim.ValueType, ClaimValueTypes.String, StringComparison.Ordinal))
				{
					writer.WriteString("valueType", claim.ValueType);
				}

				if (!string.IsNullOrEmpty(claim.Issuer) && !string.Equals(claim.Issuer, ClaimsIdentity.DefaultIssuer, StringComparison.Ordinal))
				{
					writer.WriteString("issuer", claim.Issuer);
				}

				writer.WriteEndObject();
			}

			writer.WriteEndArray();
		}

		if (value.Properties is { Count: > 0 } properties)
		{
			WritePropertyName(writer, options, "properties");
			writer.WriteStartObject();
			foreach (var property in properties)
			{
				// Attributes/Items/Claims are emitted explicitly above as top-level surfaces; skip their
				// bag entries here so they are not also written as a stringified duplicate (az9u1e).
				if (property.Key is MetadataPropertyKeys.Attributes
					or MetadataPropertyKeys.Items
					or MetadataPropertyKeys.Claims)
				{
					continue;
				}

				writer.WritePropertyName(property.Key);
				WriteObjectValue(writer, property.Value);
			}

			writer.WriteEndObject();
		}

		writer.WriteEndObject();
	}

	/// <summary>
	/// Writes a typed object bag (Attributes or Items) stored in the Properties dictionary under a
	/// well-known key as a top-level JSON object, but only when present and non-empty (so empty bags
	/// emit no spurious wire key). Values use the same AOT-safe <see cref="WriteObjectValue"/> path as
	/// the properties bag.
	/// </summary>
	private static void WriteObjectBag(
		Utf8JsonWriter writer,
		JsonSerializerOptions options,
		string wireName,
		IReadOnlyDictionary<string, object> properties,
		string bagKey)
	{
		if (!properties.TryGetValue(bagKey, out var raw)
			|| raw is not IReadOnlyDictionary<string, object> { Count: > 0 } bag)
		{
			return;
		}

		WritePropertyName(writer, options, wireName);
		writer.WriteStartObject();
		foreach (var entry in bag)
		{
			writer.WritePropertyName(entry.Key);
			WriteObjectValue(writer, entry.Value);
		}

		writer.WriteEndObject();
	}

	private static void ApplyProperty(MessageMetadataBuilder builder, string name, ref Utf8JsonReader reader, JsonSerializerOptions options)
	{
		switch (name)
		{
			case "messageId":
				_ = builder.WithMessageId(reader.GetString() ?? Guid.NewGuid().ToString());
				break;
			case "correlationId":
				_ = builder.WithCorrelationId(reader.GetString() ?? Guid.NewGuid().ToString());
				break;
			case "causationId":
				_ = builder.WithCausationId(reader.GetString());
				break;
			case "messageType":
				_ = builder.WithMessageType(reader.GetString() ?? "Unknown");
				break;
			case "contentType":
				_ = builder.WithContentType(reader.GetString() ?? "application/json");
				break;
			case "source":
				_ = builder.WithSource(reader.GetString());
				break;
			case "createdTimestampUtc":
				_ = builder.WithCreatedTimestampUtc(reader.GetDateTimeOffset());
				break;
			case "externalId":
				_ = builder.WithExternalId(reader.GetString());
				break;
			case "contentEncoding":
				_ = builder.WithContentEncoding(reader.GetString());
				break;
			case "messageVersion":
				_ = builder.WithMessageVersion(reader.GetString() ?? "1.0");
				break;
			case "serializerVersion":
				_ = builder.WithSerializerVersion(reader.GetString() ?? "1.0");
				break;
			case "contractVersion":
				_ = builder.WithContractVersion(reader.GetString() ?? "1.0.0");
				break;
			case "destination":
				_ = builder.WithDestination(reader.GetString());
				break;
			case "replyTo":
				_ = builder.WithReplyTo(reader.GetString());
				break;
			case "sessionId":
				_ = builder.WithSessionId(reader.GetString());
				break;
			case "partitionKey":
				_ = builder.WithPartitionKey(reader.GetString());
				break;
			case "routingKey":
				_ = builder.WithRoutingKey(reader.GetString());
				break;
			case "groupId":
				_ = builder.WithGroupId(reader.GetString());
				break;
			case "groupSequence":
				_ = builder.WithGroupSequence(reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64());
				break;
			case "sentTimestampUtc":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithSentTimestampUtc(reader.GetDateTimeOffset());
				}

				break;
			case "receivedTimestampUtc":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithReceivedTimestampUtc(reader.GetDateTimeOffset());
				}

				break;
			case "scheduledEnqueueTimeUtc":
				_ = builder.WithScheduledEnqueueTimeUtc(reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTimeOffset());
				break;
			case "timeToLive":
				_ = builder.WithTimeToLive(ReadOptionalTimeSpan(ref reader));
				break;
			case "expiresAtUtc":
				_ = builder.WithExpiresAtUtc(reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTimeOffset());
				break;
			case "traceParent":
				_ = builder.WithTraceParent(reader.GetString());
				break;
			case "traceState":
				_ = builder.WithTraceState(reader.GetString());
				break;
			case "baggage":
				_ = builder.WithBaggage(reader.GetString());
				break;
			case "deliveryCount":
				_ = builder.WithDeliveryCount(reader.GetInt32());
				break;
			case "maxDeliveryCount":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithMaxDeliveryCount(reader.GetInt32());
				}

				break;
			case "lastDeliveryError":
				_ = builder.WithLastDeliveryError(reader.GetString());
				break;
			case "deadLetterQueue":
				_ = builder.WithDeadLetterQueue(reader.GetString());
				break;
			case "deadLetterReason":
				_ = builder.WithDeadLetterReason(reader.GetString());
				break;
			case "deadLetterErrorDescription":
				_ = builder.WithDeadLetterErrorDescription(reader.GetString());
				break;
			case "priority":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithPriority(reader.GetInt32());
				}

				break;
			case "durable":
				_ = builder.WithDurable(reader.TokenType == JsonTokenType.Null ? null : reader.GetBoolean());
				break;
			case "requiresDuplicateDetection":
				_ = builder.WithRequiresDuplicateDetection(reader.TokenType == JsonTokenType.Null ? null : reader.GetBoolean());
				break;
			case "duplicateDetectionWindow":
				var window = ReadOptionalTimeSpan(ref reader);
				if (window.HasValue)
				{
					_ = builder.WithDuplicateDetectionWindow(window);
				}

				break;
			case "aggregateId":
				_ = builder.WithEventSourcing(aggregateId: reader.GetString());
				break;
			case "aggregateType":
				_ = builder.WithEventSourcing(aggregateType: reader.GetString());
				break;
			case "aggregateVersion":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithEventSourcing(aggregateVersion: reader.GetInt64());
				}

				break;
			case "streamName":
				_ = builder.WithEventSourcing(streamName: reader.GetString());
				break;
			case "streamPosition":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithEventSourcing(streamPosition: reader.GetInt64());
				}

				break;
			case "globalPosition":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithGlobalPosition(reader.GetInt64());
				}

				break;
			case "eventType":
				var eventType = reader.GetString();
				if (eventType != null)
				{
					_ = builder.WithEventType(eventType);
				}

				break;
			case "eventVersion":
				if (reader.TokenType != JsonTokenType.Null)
				{
					_ = builder.WithEventVersion(reader.GetInt32());
				}

				break;
			case "userId":
				_ = builder.WithUserId(reader.GetString());
				break;
			case "tenantId":
				_ = builder.WithTenantId(reader.GetString());
				break;
			case "roles":
				_ = builder.WithRoles(ReadStringArray(ref reader));
				break;
			case "headers":
				ReadHeaders(builder, ref reader);
				break;
			case "properties":
				ReadProperties(builder, ref reader, options);
				break;
			case "attributes":
				ReadObjectBag(builder, ref reader, isItems: false);
				break;
			case "items":
				ReadObjectBag(builder, ref reader, isItems: true);
				break;
			case "claims":
				ReadClaims(builder, ref reader);
				break;
			default:
				// Unknown flat key: preserve it in the properties bag using a parsed value.
				var element = JsonElement.ParseValue(ref reader);
				_ = builder.AddProperty(name, element);
				break;
		}
	}

	private static TimeSpan? ReadOptionalTimeSpan(ref Utf8JsonReader reader)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}

		var raw = reader.GetString();
		return raw is null ? null : TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
	}

	private static List<string> ReadStringArray(ref Utf8JsonReader reader)
	{
		var values = new List<string>();
		if (reader.TokenType != JsonTokenType.StartArray)
		{
			return values;
		}

		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
		{
			var item = reader.GetString();
			if (item != null)
			{
				values.Add(item);
			}
		}

		return values;
	}

	private static void ReadHeaders(MessageMetadataBuilder builder, ref Utf8JsonReader reader)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			return;
		}

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var key = reader.GetString() ?? string.Empty;
			_ = reader.Read();
			var headerValue = reader.GetString();
			if (headerValue != null)
			{
				_ = builder.AddHeader(key, headerValue);
			}
		}
	}

	private static void ReadProperties(MessageMetadataBuilder builder, ref Utf8JsonReader reader, JsonSerializerOptions options)
	{
		_ = options;
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			return;
		}

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var key = reader.GetString() ?? string.Empty;
			_ = reader.Read();
			var element = JsonElement.ParseValue(ref reader);
			_ = builder.AddProperty(key, element);
		}
	}

	/// <summary>
	/// Reads a top-level typed object bag (<c>attributes</c> or <c>items</c>) back into the builder via
	/// <see cref="MessageMetadataBuilder.AddAttributes"/> / <see cref="MessageMetadataBuilder.AddItems"/>
	/// (az9u1e). Values are kept as <see cref="JsonElement"/> mirroring the properties-bag read path.
	/// </summary>
	private static void ReadObjectBag(MessageMetadataBuilder builder, ref Utf8JsonReader reader, bool isItems)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			return;
		}

		var bag = new Dictionary<string, object>(StringComparer.Ordinal);
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var key = reader.GetString() ?? string.Empty;
			_ = reader.Read();
			bag[key] = JsonElement.ParseValue(ref reader);
		}

		if (bag.Count == 0)
		{
			return;
		}

		_ = isItems ? builder.AddItems(bag) : builder.AddAttributes(bag);
	}

	/// <summary>
	/// Reads the top-level <c>claims</c> array of <c>{type,value,valueType?,issuer?}</c> objects and
	/// reconstructs <see cref="Claim"/> instances via <see cref="MessageMetadataBuilder.WithClaims"/>
	/// (az9u1e). Omitted optional fields fall back to the BCL defaults (<see cref="ClaimValueTypes.String"/>
	/// / <see cref="ClaimsIdentity.DefaultIssuer"/>), symmetric with <c>Write</c>.
	/// </summary>
	private static void ReadClaims(MessageMetadataBuilder builder, ref Utf8JsonReader reader)
	{
		if (reader.TokenType != JsonTokenType.StartArray)
		{
			return;
		}

		var claims = new List<Claim>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				continue;
			}

			string? type = null;
			string? value = null;
			string? valueType = null;
			string? issuer = null;

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				var field = reader.GetString() ?? string.Empty;
				_ = reader.Read();
				switch (field)
				{
					case "type":
						type = reader.GetString();
						break;
					case "value":
						value = reader.GetString();
						break;
					case "valueType":
						valueType = reader.GetString();
						break;
					case "issuer":
						issuer = reader.GetString();
						break;
					default:
						_ = JsonElement.ParseValue(ref reader);
						break;
				}
			}

			if (type is null || value is null)
			{
				continue;
			}

			claims.Add(valueType is null && issuer is null
				? new Claim(type, value)
				: new Claim(type, value, valueType ?? ClaimValueTypes.String, issuer ?? ClaimsIdentity.DefaultIssuer));
		}

		if (claims.Count > 0)
		{
			_ = builder.WithClaims(claims);
		}
	}

	/// <summary>
	/// Writes a property name, honoring a caller-supplied <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>
	/// when one is set and otherwise emitting the literal name. The <paramref name="name"/> arguments
	/// passed throughout this converter are already the framework's canonical <c>camelCase</c> wire
	/// names, so with the default options (no naming policy) the serialized contract is camelCase-uniform
	/// and the in-framework wire format is preserved byte-for-byte regardless of a consumer's ambient
	/// naming policy (r5r7fe nit-6).
	/// </summary>
	private static void WritePropertyName(Utf8JsonWriter writer, JsonSerializerOptions options, string name)
	{
		var resolved = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
		writer.WritePropertyName(resolved);
	}

	private static void WriteString(Utf8JsonWriter writer, JsonSerializerOptions options, string name, string value)
	{
		WritePropertyName(writer, options, name);
		writer.WriteStringValue(value);
	}

	private static void WriteOptionalString(Utf8JsonWriter writer, JsonSerializerOptions options, string name, string? value)
	{
		if (value is null)
		{
			return;
		}

		WriteString(writer, options, name, value);
	}

	private static void WriteOptionalLong(Utf8JsonWriter writer, JsonSerializerOptions options, string name, long? value)
	{
		if (!value.HasValue)
		{
			return;
		}

		WritePropertyName(writer, options, name);
		writer.WriteNumberValue(value.Value);
	}

	private static void WriteOptionalInt(Utf8JsonWriter writer, JsonSerializerOptions options, string name, int? value)
	{
		if (!value.HasValue)
		{
			return;
		}

		WritePropertyName(writer, options, name);
		writer.WriteNumberValue(value.Value);
	}

	private static void WriteOptionalBool(Utf8JsonWriter writer, JsonSerializerOptions options, string name, bool? value)
	{
		if (!value.HasValue)
		{
			return;
		}

		WritePropertyName(writer, options, name);
		writer.WriteBooleanValue(value.Value);
	}

	private static void WriteOptionalDate(Utf8JsonWriter writer, JsonSerializerOptions options, string name, DateTimeOffset? value)
	{
		if (!value.HasValue)
		{
			return;
		}

		WritePropertyName(writer, options, name);
		writer.WriteStringValue(value.Value);
	}

	private static void WriteOptionalTimeSpan(Utf8JsonWriter writer, JsonSerializerOptions options, string name, TimeSpan? value)
	{
		if (!value.HasValue)
		{
			return;
		}

		WritePropertyName(writer, options, name);
		writer.WriteStringValue(value.Value.ToString(null, CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// Writes a property-bag value without reflection-based serialization so the converter stays
	/// AOT/trimming safe. Known primitive and well-known types are written natively; any other type
	/// falls back to its invariant string representation.
	/// </summary>
	private static void WriteObjectValue(Utf8JsonWriter writer, object? value)
	{
		switch (value)
		{
			case null:
				writer.WriteNullValue();
				break;
			case string s:
				writer.WriteStringValue(s);
				break;
			case bool b:
				writer.WriteBooleanValue(b);
				break;
			case int i:
				writer.WriteNumberValue(i);
				break;
			case long l:
				writer.WriteNumberValue(l);
				break;
			case double d:
				writer.WriteNumberValue(d);
				break;
			case decimal m:
				writer.WriteNumberValue(m);
				break;
			case float f:
				writer.WriteNumberValue(f);
				break;
			case DateTimeOffset dto:
				writer.WriteStringValue(dto);
				break;
			case DateTime dt:
				writer.WriteStringValue(dt);
				break;
			case Guid g:
				writer.WriteStringValue(g);
				break;
			case TimeSpan ts:
				writer.WriteStringValue(ts.ToString(null, CultureInfo.InvariantCulture));
				break;
			case JsonElement je:
				je.WriteTo(writer);
				break;
			case IEnumerable<string> strings:
				writer.WriteStartArray();
				foreach (var item in strings)
				{
					writer.WriteStringValue(item);
				}

				writer.WriteEndArray();
				break;
			default:
				writer.WriteStringValue(value.ToString());
				break;
		}
	}
}
