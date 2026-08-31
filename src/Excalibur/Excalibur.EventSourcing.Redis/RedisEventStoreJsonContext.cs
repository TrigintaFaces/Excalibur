// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Source-generated JSON metadata for the stream envelope persisted per event, enabling
/// serialization without runtime reflection so the append and read paths are safe under trimming
/// and ahead-of-time compilation.
/// </summary>
/// <remarks>
/// The generation options mirror the canonical event serialization contract (camelCase property
/// names, enums as strings, null values omitted) so envelopes written or read through this context
/// are byte-compatible with those produced by the reflection-based serializer.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(StoredEvent))]
internal sealed partial class RedisEventStoreJsonContext : JsonSerializerContext;
