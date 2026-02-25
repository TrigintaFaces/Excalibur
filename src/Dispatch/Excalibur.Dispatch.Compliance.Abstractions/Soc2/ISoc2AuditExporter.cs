// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides audit export operations for SOC 2 compliance data.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface contains the export operation that goes beyond core compliance
/// management. Access via <see cref="ISoc2ComplianceService.GetService(Type)"/> with
/// <c>typeof(ISoc2AuditExporter)</c>.
/// </para>
/// <para>
/// <strong>ISP Split (Sprint 551):</strong> Extracted from <see cref="ISoc2ComplianceService"/>
/// to keep the core interface at or below 5 methods per the Microsoft-First Design Standard.
/// </para>
/// </remarks>
public interface ISoc2AuditExporter
{
	/// <summary>
	/// Exports compliance data for external auditors.
	/// </summary>
	/// <param name="format">The export format.</param>
	/// <param name="periodStart">The start of the export period.</param>
	/// <param name="periodEnd">The end of the export period.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The exported data as a byte array.</returns>
	Task<byte[]> ExportForAuditorAsync(
		ExportFormat format,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken);
}
