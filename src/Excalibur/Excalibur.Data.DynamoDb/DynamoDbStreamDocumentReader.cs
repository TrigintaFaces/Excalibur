// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2.DocumentModel;

namespace Excalibur.Data.DynamoDb;

/// <summary>Converts SDK stream images using authoritative document JSON metadata.</summary>
internal sealed class DynamoDbStreamDocumentReader<TDocument>
	where TDocument : class
{
	private readonly JsonSerializerOptions _jsonOptions;

	internal DynamoDbStreamDocumentReader(IJsonTypeInfoResolver? resolver)
	{
		_jsonOptions = resolver is JsonSerializerContext context
			? new JsonSerializerOptions(context.Options)
			: new JsonSerializerOptions();
		if (resolver is null)
		{
			if (RuntimeFeature.IsDynamicCodeSupported && JsonSerializer.IsReflectionEnabledByDefault)
			{
#pragma warning disable IL2026 // Reflection metadata is reachable only when dynamic code and JSON reflection are enabled; Native AOT uses consumer metadata or the empty resolver.
				resolver = new DefaultJsonTypeInfoResolver();
#pragma warning restore IL2026
			}
			else
			{
				resolver = JsonTypeInfoResolver.Combine();
			}
		}

		_jsonOptions.TypeInfoResolver = resolver;
		_jsonOptions.MakeReadOnly();
	}

	internal TDocument? Deserialize(Dictionary<string, AttributeValue>? image)
	{
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(image);
		if (converted is null)
		{
			return null;
		}

		var document = Document.FromAttributeMap(converted);
		return JsonSerializer.Deserialize(document.ToJson(), GetTypeInfo());
	}

	private JsonTypeInfo<TDocument> GetTypeInfo()
	{
		try
		{
			if (_jsonOptions.GetTypeInfo(typeof(TDocument)) is JsonTypeInfo<TDocument> metadata)
			{
				return metadata;
			}
		}
		catch (NotSupportedException exception)
		{
			throw new NotSupportedException(
				$"JSON metadata for stream document type '{typeof(TDocument)}' is unavailable. Configure DynamoDbOptions.StreamDocumentTypeInfoResolver with source-generated metadata for this type and its members.", exception);
		}

		throw new NotSupportedException(
			$"JSON metadata for stream document type '{typeof(TDocument)}' is unavailable. Configure DynamoDbOptions.StreamDocumentTypeInfoResolver with source-generated metadata for this type and its members.");
	}
}
