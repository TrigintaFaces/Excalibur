// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Net.Mime;
using System.Text;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Provides content type negotiation and formatting for CloudEvents.
/// </summary>
public static class CloudEventContentTypes
{
	/// <summary>
	/// CloudEvents JSON format content type.
	/// </summary>
	public const string CloudEventsJson = "APPLICATION/CLOUDEVENTS+JSON";

	/// <summary>
	/// CloudEvents batch JSON format content type.
	/// </summary>
	public const string CloudEventsBatchJson = "APPLICATION/CLOUDEVENTS-BATCH+JSON";

	/// <summary>
	/// Standard JSON content type.
	/// </summary>
	public const string ApplicationJson = "APPLICATION/JSON";

	/// <summary>
	/// Binary content type for CloudEvents in HTTP binary mode.
	/// </summary>
	public const string ApplicationOctetStream = "application/octet-stream";

	/// <summary>
	/// Gets the appropriate formatter for a content type.
	/// </summary>
	/// <exception cref="NotSupportedException"></exception>
	public static JsonEventFormatter GetFormatter(string contentType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		// Normalize content type
		var mediaType = contentType.Split(';')[0].Trim().ToUpperInvariant();

		return mediaType switch
		{
			CloudEventsJson => new JsonEventFormatter(),
			ApplicationJson => new JsonEventFormatter(),
			_ => throw new NotSupportedException($"Content type '{contentType}' is not supported for CloudEvents"),
		};
	}

	/// <summary>
	/// Serializes a CloudEvent to bytes with the specified content type.
	/// </summary>
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
	public static byte[] SerializeBatch(CloudEventBatch batch, string contentType = CloudEventsBatchJson)
	{
		ArgumentNullException.ThrowIfNull(batch);

		if (contentType is not CloudEventsBatchJson and not ApplicationJson)
		{
			throw new ArgumentException($"Content type '{contentType}' is not supported for CloudEvent batches");
		}

		var formatter = new JsonEventFormatter();
		return formatter.EncodeBatchModeMessage(batch, out _).ToArray();
	}

	/// <summary>
	/// Deserializes a CloudEvent from bytes with the specified content type.
	/// </summary>
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
	public static IReadOnlyList<CloudEvent> DeserializeBatch(byte[] data, string contentType = CloudEventsBatchJson,
		params CloudEventAttribute[] extensionAttributes)
	{
		ArgumentNullException.ThrowIfNull(data);

		if (contentType is not CloudEventsBatchJson and not ApplicationJson)
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
			switch (mediaType.ToUpperInvariant())
			{
				case CloudEventsJson:
					return CloudEventsJson;

				case CloudEventsBatchJson:
					if (supportsBatch)
					{
						return CloudEventsBatchJson;
					}

					break;

				case ApplicationJson:
					return ApplicationJson;

				case "*/*":
				case "application/*":
					return CloudEventsJson;
				default:
					break;
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
