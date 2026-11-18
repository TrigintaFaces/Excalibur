// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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

// JsonElement is the shape a DECODED payload actually has: the JSON event format reader parses the
// data member into one rather than guessing a consumer type. Without it declared here, a transport
// that receives an event and forwards it fails on the forward -- and it fails only when a resolver is
// set, because a resolver chain has no reflection fallback to rescue the call. So the trim-safe
// configuration was the one that broke, which is the opposite of the order defects usually appear in.
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]

// CloudEvent extension attributes
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateTimeOffset?))]
internal partial class CloudEventJsonContext : JsonSerializerContext
{
}
