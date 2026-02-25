// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.AuditLogging.Sentinel;

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	WriteIndented = false)]
[JsonSerializable(typeof(SentinelAuditExporter.SentinelAuditPayload))]
[JsonSerializable(typeof(SentinelAuditExporter.SentinelAuditPayload[]))]
[JsonSerializable(typeof(SentinelAuditExporter.SentinelHealthPayload))]
[JsonSerializable(typeof(SentinelAuditExporter.SentinelHealthPayload[]))]
internal sealed partial class SentinelAuditJsonContext : JsonSerializerContext
{
}
