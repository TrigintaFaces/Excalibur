// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>
/// Source-generated JSON metadata for the cold-archive payload, enabling serialization without
/// runtime reflection so the archive path is safe under trimming and ahead-of-time compilation.
/// </summary>
/// <remarks>
/// The context supplies type METADATA only. The wire contract itself -- camelCase property names,
/// enums as strings, null values omitted -- is sourced from the single canonical event serializer
/// options at the point of use, so the archive format cannot drift from the rest of the framework's.
/// </remarks>
[JsonSerializable(typeof(List<StoredEvent>))]
internal sealed partial class GcsColdStoreJsonContext : JsonSerializerContext;
