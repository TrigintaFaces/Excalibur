// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud;

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	WriteIndented = false)]
[JsonSerializable(typeof(GoogleCloudLoggingAuditExporter.CloudLoggingPayload))]
[JsonSerializable(typeof(GoogleCloudLoggingAuditExporter.CloudLoggingAuditPayload))]
[ExcludeFromCodeCoverage]
internal sealed partial class GoogleCloudAuditJsonContext : JsonSerializerContext
{
}
