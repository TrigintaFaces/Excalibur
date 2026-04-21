// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Concrete DTO replacing anonymous type in <see cref="ContextFlowDiagnostics.ExportDiagnosticData"/>.
/// </summary>
internal sealed record DiagnosticExportData
{
	[JsonPropertyName("timestamp")]
	public DateTimeOffset Timestamp { get; init; }

	[JsonPropertyName("messageId")]
	public string? MessageId { get; init; }

	[JsonPropertyName("histories")]
	public ContextHistory[] Histories { get; init; } = [];

	[JsonPropertyName("recentAnomalies")]
	public ContextAnomaly[] RecentAnomalies { get; init; } = [];

	[JsonPropertyName("metricsSummary")]
	public ContextMetricsSummary? MetricsSummary { get; init; }
}
