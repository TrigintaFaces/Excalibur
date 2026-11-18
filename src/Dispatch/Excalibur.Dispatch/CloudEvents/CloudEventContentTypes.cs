// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Text;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Provides content type negotiation and formatting for CloudEvents.
/// </summary>
/// <remarks>
/// <para>
/// <b>The encode and decode members on this type serialize payloads REFLECTIVELY, and are annotated
/// accordingly.</b> They construct a parameterless CloudEvents JSON formatter, which resolves payload
/// type information at run time. Under trimming or an ahead-of-time publish that information may not
/// exist, and the failure surfaces at run time on the consumer's machine rather than at their build —
/// so the annotation is what makes the cost visible where it can still be acted on.
/// </para>
/// <para>
/// <b>The reflection-free path is <see cref="CloudEventSerializer"/>.</b> Register it with the
/// <c>AddCloudEvents</c> overload taking an <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/>
/// — typically a source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// covering the types published as CloudEvent data — and serialize through the resolved instance. That
/// overload carries no annotation, which is the whole point of the split.
/// </para>
/// <para>
/// The content-type members on this type — the constants, <see cref="NegotiateContentType"/> and
/// <see cref="CreateContentTypeHeader"/> — are pure string handling and are deliberately NOT annotated.
/// </para>
/// </remarks>
public static class CloudEventContentTypes
{
	/// <summary>
	/// CloudEvents JSON format content type.
	/// </summary>
	public const string CloudEventsJson = "application/cloudevents+json";

	/// <summary>
	/// CloudEvents batch JSON format content type.
	/// </summary>
	public const string CloudEventsBatchJson = "application/cloudevents-batch+json";

	/// <summary>
	/// Standard JSON content type.
	/// </summary>
	public const string ApplicationJson = "application/json";

	/// <summary>
	/// Binary content type for CloudEvents in HTTP binary mode.
	/// </summary>
	public const string ApplicationOctetStream = "application/octet-stream";

	/// <summary>
	/// Gets the appropriate formatter for a content type.
	/// </summary>
	/// <exception cref="NotSupportedException"></exception>
	[RequiresUnreferencedCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	public static JsonEventFormatter GetFormatter(string contentType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		// Media types are case-insensitive (RFC 2045 §5.1); compare with OrdinalIgnoreCase rather than
		// normalizing case — this avoids CA1308 (ToLowerInvariant) while honouring the lowercase
		// canonical content-type constants.
		var mediaType = contentType.Split(';')[0].Trim();

		if (mediaType.Equals(CloudEventsJson, StringComparison.OrdinalIgnoreCase)
			|| mediaType.Equals(ApplicationJson, StringComparison.OrdinalIgnoreCase))
		{
			return new JsonEventFormatter();
		}

		throw new NotSupportedException($"Content type '{contentType}' is not supported for CloudEvents");
	}

	/// <summary>
	/// Serializes a CloudEvent to bytes with the specified content type.
	/// </summary>
	[RequiresUnreferencedCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	public static byte[] Serialize(CloudEvent cloudEvent, string contentType)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		var formatter = GetFormatter(contentType);
		return formatter.EncodeStructuredModeMessage(cloudEvent, out _).ToArray();
	}

	/// <summary>
	/// Serializes a CloudEvent batch to bytes.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	[RequiresUnreferencedCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	public static byte[] SerializeBatch(CloudEventBatch batch, string contentType = CloudEventsBatchJson)
	{
		ArgumentNullException.ThrowIfNull(batch);

		if (!string.Equals(contentType, CloudEventsBatchJson, StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(contentType, ApplicationJson, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException($"Content type '{contentType}' is not supported for CloudEvent batches");
		}

		var formatter = new JsonEventFormatter();
		return formatter.EncodeBatchModeMessage(batch, out _).ToArray();
	}

	/// <summary>
	/// Deserializes a CloudEvent from bytes with the specified content type.
	/// </summary>
	[RequiresUnreferencedCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	public static CloudEvent Deserialize(byte[] data, string contentType, params CloudEventAttribute[] extensionAttributes)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		var formatter = GetFormatter(contentType);
		return formatter.DecodeStructuredModeMessage(data, new ContentType(contentType), extensionAttributes);
	}

	/// <summary>
	/// Deserializes a CloudEvent batch from bytes.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	[RequiresUnreferencedCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvent payloads are serialized reflectively on this path. Use the registered "
		+ "CloudEventSerializer, built by AddCloudEvents(IJsonTypeInfoResolver) over a "
		+ "source-generated context covering the types you publish as CloudEvent data.")]
	public static IReadOnlyList<CloudEvent> DeserializeBatch(byte[] data, string contentType = CloudEventsBatchJson,
		params CloudEventAttribute[] extensionAttributes)
	{
		ArgumentNullException.ThrowIfNull(data);

		if (!string.Equals(contentType, CloudEventsBatchJson, StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(contentType, ApplicationJson, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException($"Content type '{contentType}' is not supported for CloudEvent batches");
		}

		var formatter = new JsonEventFormatter();
		using var stream = new MemoryStream(data);
		return formatter.DecodeBatchModeMessage(stream, new ContentType(contentType), extensionAttributes);
	}

	/// <summary>
	/// Determines the best content type based on Accept headers.
	/// </summary>
	public static string NegotiateContentType(string? acceptHeader, bool supportsBatch = false)
	{
		if (string.IsNullOrWhiteSpace(acceptHeader))
		{
			return CloudEventsJson;
		}

		var acceptTypes = ParseAcceptHeader(acceptHeader);

		foreach (var (mediaType, _) in acceptTypes)
		{
			// Media types are case-insensitive (RFC 2045 §5.1).
			if (mediaType.Equals(CloudEventsJson, StringComparison.OrdinalIgnoreCase))
			{
				return CloudEventsJson;
			}

			if (mediaType.Equals(CloudEventsBatchJson, StringComparison.OrdinalIgnoreCase))
			{
				if (supportsBatch)
				{
					return CloudEventsBatchJson;
				}
			}
			else if (mediaType.Equals(ApplicationJson, StringComparison.OrdinalIgnoreCase))
			{
				return ApplicationJson;
			}
			else if (string.Equals(mediaType, "*/*", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(mediaType, "application/*", StringComparison.OrdinalIgnoreCase))
			{
				return CloudEventsJson;
			}
		}

		// Default to CloudEvents JSON
		return CloudEventsJson;
	}

	/// <summary>
	/// Creates a content type header with additional parameters.
	/// </summary>
	public static string CreateContentTypeHeader(string mediaType, string? charset = "utf-8", Dictionary<string, string>? parameters = null)
	{
		var sb = new StringBuilder(mediaType);

		if (!string.IsNullOrWhiteSpace(charset))
		{
			_ = sb.Append(CultureInfo.InvariantCulture, $"; charset={charset}");
		}

		if (parameters != null)
		{
			foreach (var (key, value) in parameters)
			{
				_ = sb.Append(CultureInfo.InvariantCulture, $"; {key}={value}");
			}
		}

		return sb.ToString();
	}

	private static List<(string mediaType, double quality)> ParseAcceptHeader(string acceptHeader)
	{
		var types = new List<(string, double)>();
		var parts = acceptHeader.Split(',', StringSplitOptions.RemoveEmptyEntries);

		foreach (var part in parts)
		{
			var components = part.Split(';', StringSplitOptions.RemoveEmptyEntries);
			var mediaType = components[0].Trim();
			var quality = 1.0;

			// Look for quality factor
			foreach (var component in components.Skip(1))
			{
				var trimmed = component.Trim();
				if (trimmed.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
				{
					if (double.TryParse(trimmed.AsSpan(2), out var q))
					{
						quality = Math.Max(0, Math.Min(1, q));
					}

					break;
				}
			}

			types.Add((mediaType, quality));
		}

		// Sort by quality descending
		types.Sort(static (a, b) => b.Item2.CompareTo(a.Item2));
		return types;
	}
}
