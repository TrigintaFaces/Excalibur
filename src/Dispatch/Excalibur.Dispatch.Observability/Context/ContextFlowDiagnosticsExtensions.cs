// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Extension methods for <see cref="IContextFlowDiagnostics"/>.
/// </summary>
public static class ContextFlowDiagnosticsExtensions
{
	/// <summary>Gets recent anomalies.</summary>
	public static IEnumerable<ContextAnomaly> GetRecentAnomalies(this IContextFlowDiagnostics diagnostics, int limit = 100)
	{
		ArgumentNullException.ThrowIfNull(diagnostics);
		return ((ContextFlowDiagnostics)diagnostics).GetRecentAnomalies(limit);
	}

	/// <summary>Generates a correlation report.</summary>
	public static string GenerateCorrelationReport(this IContextFlowDiagnostics diagnostics, string correlationId)
	{
		ArgumentNullException.ThrowIfNull(diagnostics);
		return ((ContextFlowDiagnostics)diagnostics).GenerateCorrelationReport(correlationId);
	}

	/// <summary>Exports diagnostic data.</summary>
	public static string ExportDiagnosticData(this IContextFlowDiagnostics diagnostics, string? messageId = null)
	{
		ArgumentNullException.ThrowIfNull(diagnostics);
		return ((ContextFlowDiagnostics)diagnostics).ExportDiagnosticData(messageId);
	}
}
