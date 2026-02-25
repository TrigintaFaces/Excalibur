// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;


namespace Excalibur.Dispatch.Compliance;


[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	WriteIndented = true,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(Soc2ReportExporter.Soc2ReportExportModel))]
[JsonSerializable(typeof(Soc2ReportExporter.Soc2ReportExportSection))]
[JsonSerializable(typeof(Soc2ReportExporter.EvidenceManifest))]
[JsonSerializable(typeof(Soc2ReportExporter.ManifestItem))]
internal sealed partial class Soc2ReportJsonContext : JsonSerializerContext
{
}
