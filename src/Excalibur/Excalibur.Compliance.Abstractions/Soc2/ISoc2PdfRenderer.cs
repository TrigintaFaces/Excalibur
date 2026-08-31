// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Renders a SOC 2 report as a PDF document.
/// </summary>
/// <remarks>
/// <para>
/// PDF rendering is not part of <c>Excalibur.Compliance</c>. Install the <c>Excalibur.Compliance.Pdf</c>
/// package and call <c>services.AddSoc2PdfExport()</c> to enable <see cref="ExportFormat.Pdf"/>.
/// Without it the report exporter reports PDF as unsupported and rejects a PDF export request; every
/// other export format is unaffected.
/// </para>
/// <para>
/// The PDF package depends on QuestPDF, whose Community edition is free only for licensees below
/// USD 1,000,000 total annual gross revenue measured company-wide. Above that threshold a paid
/// QuestPDF licence is required. That obligation reaches only consumers who install the PDF package.
/// </para>
/// </remarks>
public interface ISoc2PdfRenderer
{
	/// <summary>
	/// Renders the report as a PDF document.
	/// </summary>
	/// <param name="report">The report to render.</param>
	/// <param name="options">Export options; the PDF-specific settings live on <see cref="Soc2ReportExportOptions.PdfOptions"/>.</param>
	/// <returns>The rendered PDF bytes.</returns>
	byte[] Render(Soc2Report report, Soc2ReportExportOptions options);
}
