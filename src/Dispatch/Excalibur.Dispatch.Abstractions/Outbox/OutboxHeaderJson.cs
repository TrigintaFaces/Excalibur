// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Outbox;

/// <summary>Reads the closed persisted header object without discovering payload types.</summary>
internal static class OutboxHeaderJson
{
	// Null/empty guards and fallback dictionaries belong to each persisted format's caller.
	// Non-null object values remain JsonElement, including numbers, arrays and nested objects.
	internal static Dictionary<string, object>? Deserialize(string json) =>
		JsonSerializer.Deserialize(json, OutboxHeaderJsonContext.Default.DictionaryStringObject);
}

[JsonSerializable(typeof(Dictionary<string, object>))]
internal sealed partial class OutboxHeaderJsonContext : JsonSerializerContext;
