// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.A3.Audit;

/// <summary>
/// Source-generated JSON serializer context for A3 audit types.
/// Enables AOT-safe serialization for audit middleware outbox fallback.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(RaisedBy))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class AuditJsonContext : JsonSerializerContext;
