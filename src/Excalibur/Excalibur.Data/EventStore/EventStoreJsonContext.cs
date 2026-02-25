// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data.EventStore;

/// <summary>
/// Source-generated JSON serialization context for Event Store types. This provides AOT-compatible JSON serialization for event sourcing.
/// </summary>
[JsonSerializable(typeof(EventMetadata))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<EventMetadata>))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
public partial class EventStoreJsonContext : JsonSerializerContext
{
	/// <summary>
	/// Gets the singleton instance of the EventStoreJsonContext.
	/// </summary>
	/// <value>
	/// The singleton instance of the EventStoreJsonContext.
	/// </value>
	[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
	public static EventStoreJsonContext Instance { get; } = new(new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() },
	});
}
