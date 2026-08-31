// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Pdf;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Logging.Abstractions;

using QuestPDF.Infrastructure;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Locks the consumer obligation that the framework never writes the process-global
/// <c>QuestPDF.Settings.License</c>. It is a static shared with the whole application, so writing it
/// would overwrite a Professional or Enterprise license the host configured and would assert
/// Community eligibility on the host's behalf.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Collection(QuestPdfLicenseCollection.Name)]
public sealed class Soc2PdfExportShould
{
	[Fact]
	public async Task NotOverwriteTheHostConfiguredQuestPdfLicense()
	{
		// Arrange: the host has already selected a license other than Community.
		var original = QuestPDF.Settings.License;
		QuestPDF.Settings.License = LicenseType.Enterprise;

		try
		{
			var sut = new Soc2ReportExporter(
				NullLogger<Soc2ReportExporter>.Instance,
				TimeProvider.System,
				new QuestPdfSoc2PdfRenderer(TimeProvider.System));

			// Act
			var result = await sut.ExportAsync(CreateReport(), ExportFormat.Pdf, null, CancellationToken.None);

			// Assert: a PDF was produced, and the host's license selection survived it.
			result.Data.ShouldNotBeEmpty();
			QuestPDF.Settings.License.ShouldBe(LicenseType.Enterprise);
		}
		finally
		{
			QuestPDF.Settings.License = original;
		}
	}

	private static Soc2Report CreateReport() =>
		new()
		{
			ReportId = Guid.NewGuid(),
			Title = "License Isolation Report",
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			System = new SystemDescription
			{
				Name = "Test System",
				Description = "A test system",
				Services = ["Service A"],
				Infrastructure = ["Server 1"],
				DataTypes = ["PII"]
			},
			ControlSections = [],
			CategoriesIncluded = [TrustServicesCategory.Security]
		};
}
