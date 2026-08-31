// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Pdf;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SOC 2 PDF export.
/// </summary>
public static class Soc2PdfServiceCollectionExtensions
{
	/// <summary>
	/// Adds PDF rendering for SOC 2 report export.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Call this alongside <c>AddSoc2Compliance()</c> to enable <see cref="ExportFormat.Pdf"/>. Without it
	/// the report exporter reports PDF as unsupported and rejects a PDF export request; every other export
	/// format works unchanged.
	/// </para>
	/// <para>
	/// This package depends on QuestPDF, whose Community edition is free only for licensees below
	/// USD 1,000,000 total annual gross revenue measured company-wide. Above that threshold a paid QuestPDF
	/// licence is required. The hosting application selects the licence itself, for example
	/// <c>QuestPDF.Settings.License = LicenseType.Community;</c> at startup; this package never writes that
	/// process-global static on the application's behalf.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSoc2PdfExport(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton(TimeProvider.System);
		services.TryAddScoped<ISoc2PdfRenderer, QuestPdfSoc2PdfRenderer>();

		return services;
	}
}
