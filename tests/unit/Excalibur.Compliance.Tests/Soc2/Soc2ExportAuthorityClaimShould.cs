// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Compliance;
using Excalibur.Compliance.Pdf;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// The exported SOC 2 artifact must not label anything in it as an auditor's opinion.
/// </summary>
/// <remarks>
/// <para>
/// A SOC 2 opinion is issued by an independent practitioner following an examination. This library
/// performs no examination and is not that party, so an artifact it produces may not carry that
/// vocabulary — the artifact's audience is external, and a misstatement in it is a finding against
/// the CONSUMER rather than against us.
/// </para>
/// <para>
/// This asserts the EXPORTED BYTES, not the model. The report type could be renamed while an
/// exporter kept writing "Opinion:" into the text a reader actually sees, so the model is the
/// wrong subject: only the serialized artifact answers the question being asked.
/// </para>
/// <para>
/// NON-VACUITY: the liveness arm below proves the same bytes DO carry the level we did determine,
/// so the safety arms cannot be satisfied by an exporter that emits nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ExportAuthorityClaimShould
{
	// Whole words, so "opinion" does not match inside an unrelated token and the arm cannot pass by
	// accident of spelling.
	private static readonly string[] AuthorityClaimTerms =
	[
		"opinion", "auditor", "unqualified", "qualified", "adverse", "disclaimer"
	];

	private readonly Soc2ReportExporter _sut = new(
		NullLogger<Soc2ReportExporter>.Instance,
		TimeProvider.System,
		new QuestPdfSoc2PdfRenderer(TimeProvider.System));

	/// <summary>SAFETY: no text-bearing export format may claim third-party authority.</summary>
	[Theory]
	[InlineData(ExportFormat.Json)]
	[InlineData(ExportFormat.Xml)]
	[InlineData(ExportFormat.Text)]
	public async Task NotClaimThirdPartyAuthorityInAnyTextFormat(ExportFormat format)
	{
		var result = await _sut.ExportAsync(CreateReport(), format, null, CancellationToken.None)
			.ConfigureAwait(false);

		var exported = Encoding.UTF8.GetString(result.Data);

		foreach (var term in AuthorityClaimTerms)
		{
			exported.ShouldNotContain(
				term,
				Case.Insensitive,
				$"the exported {format} artifact is handed to an assessor, so it must not contain "
				+ $"'{term}' — a term that asserts a judgement only a licensed third party may make");
		}
	}

	/// <summary>
	/// LIVENESS. Without this the safety arms above are satisfied by an exporter that writes an empty
	/// document: nothing trivially claims no authority.
	/// </summary>
	[Theory]
	[InlineData(ExportFormat.Json)]
	[InlineData(ExportFormat.Xml)]
	[InlineData(ExportFormat.Text)]
	public async Task StillReportTheLevelThisLibraryDetermined(ExportFormat format)
	{
		var report = CreateReport() with { OverallLevel = ComplianceLevel.PartiallyCompliant };

		var result = await _sut.ExportAsync(report, format, null, CancellationToken.None)
			.ConfigureAwait(false);

		var exported = Encoding.UTF8.GetString(result.Data);

		exported.ShouldContain(
			nameof(ComplianceLevel.PartiallyCompliant),
			Case.Insensitive,
			"removing the auditor vocabulary must not remove the verdict — the consumer still needs "
			+ "the level our own checks produced");
	}

	private static Soc2Report CreateReport() =>
		new()
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "Test SOC 2 Report",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = new SystemDescription
			{
				Name = "Test System",
				Description = "A test system",
				Services = ["Service A"],
				Infrastructure = ["Server 1"],
				DataTypes = ["PII"]
			},
			ControlSections = [],
			OverallLevel = ComplianceLevel.FullyCompliant,
			GeneratedAt = DateTimeOffset.UtcNow
		};
}
