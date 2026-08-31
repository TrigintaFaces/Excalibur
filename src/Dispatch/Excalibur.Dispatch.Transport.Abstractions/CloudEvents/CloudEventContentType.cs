// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Http.Headers;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Classifies a message content type so every transport adapter reaches the same decode decision.
/// </summary>
/// <remarks>
/// Media types are case-insensitive and may carry parameters (RFC 9110 section 8.3), so
/// <c>application/cloudevents+json; charset=utf-8</c>, <c>APPLICATION/CLOUDEVENTS+JSON</c> and
/// <c>application/cloudevents+json</c> all denote the same type and must decode identically.
/// </remarks>
internal static class CloudEventContentType
{
	/// <summary> The structured-mode CloudEvents media type. </summary>
	internal const string StructuredJson = "application/cloudevents+json";

	/// <summary>
	/// Gets the bare media type, with any parameters removed, or <see langword="null" /> when the value is absent.
	/// </summary>
	/// <param name="contentType"> The raw content-type header value. </param>
	/// <returns> The parameter-free media type, or <see langword="null" />. </returns>
	internal static string? MediaType(string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			return null;
		}

		if (MediaTypeHeaderValue.TryParse(contentType, out var parsed) && parsed.MediaType is { Length: > 0 } mediaType)
		{
			return mediaType;
		}

		// Not a well-formed header; fall back to the portion before the first parameter separator.
		var separator = contentType.IndexOf(';', StringComparison.Ordinal);
		var bare = (separator < 0 ? contentType : contentType[..separator]).Trim();

		return bare.Length == 0 ? null : bare;
	}

	/// <summary>
	/// Determines whether the content type denotes the supplied media type, ignoring case and parameters.
	/// </summary>
	/// <param name="contentType"> The raw content-type header value. </param>
	/// <param name="mediaType"> The media type to compare against. </param>
	/// <returns> <see langword="true" /> when the two denote the same media type. </returns>
	internal static bool Is(string? contentType, string mediaType) =>
		string.Equals(MediaType(contentType), mediaType, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Determines whether the content type denotes a JSON payload, including the <c>+json</c> structured suffix
	/// (RFC 6839) used by <c>application/cloudevents+json</c>.
	/// </summary>
	/// <param name="contentType"> The raw content-type header value. </param>
	/// <returns> <see langword="true" /> when the payload should be parsed as JSON. </returns>
	internal static bool IsJson(string? contentType)
	{
		var mediaType = MediaType(contentType);

		// Covers application/json and every +json structured suffix, application/cloudevents+json included.
		return mediaType?.EndsWith("json", StringComparison.OrdinalIgnoreCase) == true;
	}

	/// <summary>
	/// Determines whether the content type denotes a structured-mode CloudEvent.
	/// </summary>
	/// <param name="contentType"> The raw content-type header value. </param>
	/// <returns> <see langword="true" /> when the message carries a structured-mode CloudEvent. </returns>
	internal static bool IsStructured(string? contentType) =>
		MediaType(contentType)?.StartsWith("application/cloudevents", StringComparison.OrdinalIgnoreCase) == true;
}
