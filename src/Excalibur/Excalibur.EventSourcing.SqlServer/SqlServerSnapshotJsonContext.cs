// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

using System.Text.Json;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// Source-generated JSON metadata for the snapshot metadata envelope, enabling deserialization
/// without runtime reflection so the snapshot read path is safe under trimming and ahead-of-time
/// compilation.
/// </summary>
/// <remarks>
/// No generation options are declared, so the emitted contract matches the serializer defaults used
/// by the reflection-based path this replaces. Dictionary keys are written verbatim under both, so
/// stored metadata remains readable unchanged.
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(IDictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class SqlServerSnapshotJsonContext : JsonSerializerContext;
