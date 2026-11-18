// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;

namespace Excalibur.Outbox;

/// <summary>Store-owned generated metadata writer; it never selects a reflection fallback.</summary>
internal sealed class OutboxMetadataWriter
{
	private readonly JsonSerializerOptions _options;

	private OutboxMetadataWriter(JsonSerializerOptions options) => _options = options;

	internal static OutboxMetadataWriter Create(Func<JsonSerializerOptions, IJsonTypeInfoResolver> resolverFactory)
	{
		ArgumentNullException.ThrowIfNull(resolverFactory);

		var options = EventSerializationDefaults.CreateCanonicalOptionsForSourceGeneration();
		var resolver = resolverFactory(options) ?? throw new InvalidOperationException(
			"The outbox metadata resolver factory returned null. Return the generated context bound to the supplied options.");

		ValidateCanonicalSettings(options);
		if (resolver is JsonSerializerContext context && !ReferenceEquals(context.Options, options))
		{
			throw new InvalidOperationException(
				"The outbox metadata context must be constructed with the supplied canonical options; a context's Default instance is bound to different options.");
		}

		if (options.IsReadOnly)
		{
			if (!ReferenceEquals(options.TypeInfoResolver, resolver))
			{
				throw new InvalidOperationException(
					"The outbox metadata factory froze the supplied options with a resolver other than the one it returned.");
			}
		}
		else
		{
			options.TypeInfoResolver = resolver;
			options.MakeReadOnly();
		}

		return new OutboxMetadataWriter(options);
	}

	internal string Serialize(IDictionary<string, object> metadata) =>
		Encoding.UTF8.GetString(EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _options));

	internal OutboxMessage FromOutboundMessage(OutboundMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);
		var headers = message.Headers ?? new Dictionary<string, object>(StringComparer.Ordinal);
		return OutboxMessage.FromOutboundMessage(message, Serialize(headers));
	}

	private static void ValidateCanonicalSettings(JsonSerializerOptions options)
	{
		// The callback provides metadata, not a second independently configurable wire contract.
		// Attribute/generated converters remain valid; adding option-wide converters is not canonical.
		if (!ReferenceEquals(options.PropertyNamingPolicy, JsonNamingPolicy.CamelCase)
			|| options.DefaultIgnoreCondition != JsonIgnoreCondition.WhenWritingNull
			|| options.DictionaryKeyPolicy is not null
			|| options.MaxDepth != 0
			|| options.WriteIndented
			|| options.Encoder is not null
			|| options.NumberHandling != JsonNumberHandling.Strict
			|| options.ReferenceHandler is not null
			|| options.IncludeFields
			|| options.IgnoreReadOnlyFields
			|| options.IgnoreReadOnlyProperties
			|| options.Converters.Count != 0)
		{
			throw new InvalidOperationException(
				"The outbox metadata resolver factory must preserve canonical JSON settings. Configure enum and custom value converters in generated type metadata.");
		}
	}
}
