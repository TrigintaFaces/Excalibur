// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Source generation context for core Dispatch message types. Provides AOT-compatible JSON serialization for the messaging framework.
/// </summary>
/// <remarks>
/// This context includes all core message types used throughout the Excalibur framework. For cloud provider-specific types, use the
/// respective provider contexts.
/// </remarks>
// AOT source-gen MIRROR of the canonical event wire-shape (EventSerializationDefaults.Canonical): the three
// options below — CamelCase + WhenWritingNull + UseStringEnumConverter — reproduce the reflection default's
// wire format so a Native-AOT host (this source-gen path) and a JIT host (the reflection Canonical) serialize
// events byte-for-byte identically. Keep these in lockstep with EventSerializationDefaults if the canonical
// contract ever changes.
[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true,
	MaxDepth = 32)]

// Core message types - Note: MessageEnvelope<T> is generic and requires specific type registration
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(object))]

// A Dictionary<string, object> that has been through a JSON round-trip holds JsonElement values, so the
// dictionary entry above is only serializable in one direction without this one. Writing such a dictionary
// resolves a converter for each value's RUNTIME type, and without JsonElement declared here that resolution
// throws NotSupportedException on a dictionary this context claims to support.
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(Guid))]
internal partial class CoreMessageJsonContext : JsonSerializerContext
{
	// ONE context, ONE option set. This type used to carry a second instance built from a private
	// options object that set three things the attribute above did not, and the two were reachable by
	// different routes to the same public serializer -- so whether a message was accepted depended on
	// which constructor overload the caller used. The three settings were ruled on rather than dropped
	// silently:
	//
	//   MaxDepth = 32                       KEPT, and moved onto the attribute above. It is a
	//                                       deliberately stricter nesting bound than the framework's
	//                                       64, so convergence must not cost it. Verified in the
	//                                       generated context that an attribute-level MaxDepth lands
	//                                       in the shared options rather than in a per-type converter.
	//   ReferenceHandler = IgnoreCycles     DROPPED, and this tightens behaviour on purpose. It wrote
	//                                       a null in place of a cycle, so a message serialized with a
	//                                       cycle lost a field and nothing downstream learned. Without
	//                                       it a cycle throws, which is loud and recoverable.
	//   NumberHandling.AllowReadingFromString
	//                                       DROPPED. It accepted a JSON string where the type declares
	//                                       a number -- a wire shape our own writer never emits -- so
	//                                       it only ever widened what one route would deserialize.
	//                                       Strict is the framework default and the honest contract.
}
