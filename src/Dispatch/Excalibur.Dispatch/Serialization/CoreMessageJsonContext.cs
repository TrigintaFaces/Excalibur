// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Source generation context for core Dispatch message types. Provides AOT-compatible JSON serialization for the messaging framework.
/// </summary>
/// <remarks>
/// This context includes all core message types used throughout the Dispatch framework. For cloud provider-specific types, use the
/// respective provider contexts.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true)]

// Core message types - Note: MessageEnvelope<T> is generic and requires specific type registration
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(Guid))]
public partial class CoreMessageJsonContext : JsonSerializerContext
{
	/// <summary>
	/// Gets the singleton instance for core message serialization.
	/// </summary>
	/// <value>
	/// The singleton instance for core message serialization.
	/// </value>
	public static CoreMessageJsonContext Instance { get; } = new(GetDefaultOptions());

	/// <summary>
	/// Creates default JSON serializer options for core messages.
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
			MaxDepth = 32, // Messages can have nested structures
		};

		return options;
	}
}
