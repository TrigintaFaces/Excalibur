// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Extension methods for CloudEvent content type handling.
/// </summary>
public static class CloudEventContentTypeExtensions
{
	/// <summary>
	/// Converts a CloudEvent to JSON string.
	/// </summary>
	public static string ToJson(this CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		var bytes = CloudEventContentTypes.Serialize(cloudEvent, CloudEventContentTypes.CloudEventsJson);
		return Encoding.UTF8.GetString(bytes);
	}

	/// <summary>
	/// Converts a CloudEvent to JSON string with custom serialization options.
	/// </summary>
	public static string ToJson(this CloudEvent cloudEvent, JsonSerializerOptions? options)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		_ = options; // Reserved for future use

		var bytes = CloudEventContentTypes.Serialize(cloudEvent, CloudEventContentTypes.CloudEventsJson);
		return Encoding.UTF8.GetString(bytes);
	}

	/// <summary>
	/// Converts a CloudEvent batch to JSON string.
	/// </summary>
	public static string ToJson(this CloudEventBatch batch)
	{
		ArgumentNullException.ThrowIfNull(batch);

		var bytes = CloudEventContentTypes.SerializeBatch(batch);
		return Encoding.UTF8.GetString(bytes);
	}

	/// <summary>
	/// Converts a CloudEvent batch to JSON string with custom serialization options.
	/// </summary>
	public static string ToJson(this CloudEventBatch batch, JsonSerializerOptions? options)
	{
		ArgumentNullException.ThrowIfNull(batch);
		_ = options; // Reserved for future use

		var bytes = CloudEventContentTypes.SerializeBatch(batch);
		return Encoding.UTF8.GetString(bytes);
	}

	/// <summary>
	/// Creates a CloudEvent from JSON string.
	/// </summary>
	public static CloudEvent ParseCloudEvent(string json, params CloudEventAttribute[] extensionAttributes)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(json);

		var bytes = Encoding.UTF8.GetBytes(json);
		return CloudEventContentTypes.Deserialize(bytes, CloudEventContentTypes.CloudEventsJson, extensionAttributes);
	}

	/// <summary>
	/// Creates a CloudEvent batch from JSON string.
	/// </summary>
	public static IReadOnlyList<CloudEvent> ParseCloudEventBatch(string json, params CloudEventAttribute[] extensionAttributes)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(json);

		var bytes = Encoding.UTF8.GetBytes(json);
		return CloudEventContentTypes.DeserializeBatch(bytes, CloudEventContentTypes.CloudEventsBatchJson, extensionAttributes);
	}
}
