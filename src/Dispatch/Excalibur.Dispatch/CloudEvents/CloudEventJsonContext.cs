// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Source generation context for CloudEvents serialization. Provides AOT-compatible JSON serialization for CloudEvents specification 1.0.
/// </summary>
/// <remarks>
/// This context is optimized for CloudEvents processing across all cloud providers with:
/// - Standard CloudEvents attribute serialization
/// - Extension attribute support
/// - Binary and structured content mode support.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true)]

// CloudEvents types
[JsonSerializable(typeof(CloudEvent))]
[JsonSerializable(typeof(CloudEventBatch))]
[JsonSerializable(typeof(List<CloudEvent>))]
[JsonSerializable(typeof(CloudEvent[]))]

// Common CloudEvent data types
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(ReadOnlyMemory<byte>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]

// CloudEvent extension attributes
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateTimeOffset?))]
public partial class CloudEventJsonContext : JsonSerializerContext
{
	/// <summary>
	/// Gets the singleton instance for CloudEvents serialization.
	/// </summary>
	/// <value>
	/// The singleton instance for CloudEvents serialization.
	/// </value>
	public static CloudEventJsonContext Instance { get; } = new(GetDefaultOptions());

	/// <summary>
	/// Creates default JSON serializer options for CloudEvents.
	/// </summary>
	private static JsonSerializerOptions GetDefaultOptions()
	{
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			ReferenceHandler = ReferenceHandler.IgnoreCycles,
			NumberHandling = JsonNumberHandling.AllowReadingFromString,
			MaxDepth = 16, // CloudEvents typically have shallow structure
		};

		return options;
	}
}
