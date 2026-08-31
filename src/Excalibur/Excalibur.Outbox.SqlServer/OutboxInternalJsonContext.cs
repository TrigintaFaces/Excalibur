// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Source-generated JSON metadata for the closed payload shapes this package reads and writes
/// itself, enabling serialization without runtime reflection so those paths are safe under trimming
/// and ahead-of-time compilation.
/// </summary>
/// <remarks>
/// A dictionary with string values is written the same way under any property naming policy,
/// because a naming policy renames properties and not dictionary keys. Stored dead-letter metadata
/// therefore remains readable unchanged.
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class OutboxInternalJsonContext : JsonSerializerContext;
