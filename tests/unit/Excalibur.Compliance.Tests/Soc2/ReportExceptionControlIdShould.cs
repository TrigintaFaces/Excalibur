// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// One property: <b>a finding never invents a control it cannot name.</b>
/// </summary>
/// <remarks>
/// <para>
/// A finding about a criterion as a whole names no single control, and until <c>ControlId</c> could be
/// absent the producers wrote the string <c>"N/A"</c>. An assessor reads that as a control whose
/// identifier is the letters N/A, and a tool reads it as a value. Neither reads it as "there was no
/// control", which is the only true statement available.
/// </para>
/// <para>
/// The arms are paired deliberately. Asserting only that a criterion-level finding carries no control
/// would be satisfied by a producer that carried no control on ANY finding — so a liveness arm asserts
/// a control-level finding still names its control. Absence has to be selective to mean anything.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ReportExceptionControlIdShould
{
	/// <summary>The placeholder this change exists to remove. Asserted by its literal text.</summary>
	private const string RemovedPlaceholder = "N/A";

	[Fact]
	public async Task EmitNoControl_WhenTheGeneratorRaisesACriterionLevelFinding()
	{
		// SAFETY, and it drives the REAL generator rather than a hand-built record. An arm that
		// constructs a ReportException with ControlId = null and then asserts it is null tests C#
		// object initialisation; it passes against the unfixed producers and proves nothing. The
		// property that matters is what the PRODUCER emits.
		// One criterion has a failing control; the rest have no validator, so the same report carries
		// BOTH producers at once -- a control-level finding and a set of unassessed ones. Asserting
		// over the whole list is stronger than picking one entry, and it is what a reader of the
		// report actually sees.
		var report = await GenerateAsync(Ineffective("CC6.1")).ConfigureAwait(false);

		report.Exceptions.ShouldNotBeEmpty("the generator must produce findings, or this arm is vacuous.");

		report.Exceptions
			.Select(static e => e.ControlId)
			.ShouldNotContain(
				RemovedPlaceholder,
				"no finding may invent a control identifier. Asserted against the exact removed string, "
				+ "so a different placeholder fails visibly rather than slipping past a pattern.");

		// The unassessed criteria name no control -- nothing was examined, so there is none to name.
		report.Exceptions
			.Where(static e => e.Description.Contains("NOT ASSESSED", StringComparison.Ordinal))
			.ShouldAllBe(static e => e.ControlId == null);

		// LIVENESS in the same report: the finding that IS about a control still names it, so a
		// producer that nulled every identifier could not pass the assertion above.
		report.Exceptions
			.Select(static e => e.ControlId)
			.ShouldContain("CC6.1");
	}

	[Fact]
	public void HoldTheAbsence_WhenAFindingNamesNoControl()
	{
		// The codomain itself: the type can represent "no control". Kept deliberately small and
		// honest about what it is -- a type-level check, not a claim about any producer.
		var exception = CriterionLevelFinding();

		exception.ControlId.ShouldBeNull();
	}

	[Fact]
	public void StillNameTheControl_WhenTheFindingIsAboutOne()
	{
		// LIVENESS for the codomain: without this, making ControlId unconditionally null would satisfy
		// every absence assertion above while destroying the attribution an assessor needs.
		var exception = ControlLevelFinding("CC6.1");

		exception.ControlId.ShouldBe("CC6.1");
	}

	[Fact]
	public async Task OmitTheControlElementFromXml_RatherThanEmitItEmpty()
	{
		// An empty <controlId/> asserts that a control exists whose identifier is the empty string.
		// Absence of the element asserts nothing, which is the true statement.
		var xml = await ExportAsync(ExportFormat.Xml, CriterionLevelFinding()).ConfigureAwait(false);

		// An empty element is a claim about a control that does not exist; the placeholder is the
		// claim this change removes. Neither may reach exported output.
		xml.ShouldNotContain("<controlId></controlId>");
		xml.ShouldNotContain($"<controlId>{RemovedPlaceholder}</controlId>");
	}

	[Fact]
	public async Task StillEmitTheControlElement_WhenTheFindingNamesAControl()
	{
		// LIVENESS for the XML arm: a fix that omitted the element unconditionally would pass above.
		var xml = await ExportAsync(ExportFormat.Xml, ControlLevelFinding("CC6.1")).ConfigureAwait(false);

		// Omitting the element must be conditional on there being no control, not unconditional.
		xml.ShouldContain("<controlId>CC6.1</controlId>");
	}

	/// <summary>
	/// Drives the real <see cref="Soc2ReportGenerator"/> so the assertion is about what a PRODUCER
	/// emits, not about a record the test populated itself.
	/// </summary>
	private static async Task<Soc2Report> GenerateAsync(params ControlValidationResult[] results)
	{
		var validation = A.Fake<IControlValidationService>();
		A.CallTo(() => validation.ValidateCriterionAsync(
				TrustServicesCriterion.CC6_LogicalAccess, A<CancellationToken>._))
			.Returns(results.ToList());

		var options = new Soc2Options
		{
			EnabledCategories = [TrustServicesCategory.Security],
			MinimumTypeIIPeriodDays = 90,
			DefaultTestSampleSize = 25
		};

		var generator = new Soc2ReportGenerator(
			Microsoft.Extensions.Options.Options.Create(options),
			validation,
			NullLogger<Soc2ReportGenerator>.Instance);

		return await generator
			.GenerateTypeIReportAsync(DateTimeOffset.UnixEpoch, new ReportOptions(), CancellationToken.None)
			.ConfigureAwait(false);
	}

	private static ControlValidationResult Ineffective(string id) =>
		new()
		{
			ControlId = id,
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 20,
			ConfigurationIssues = [$"A violation was detected in {id}."],
			ValidatedAt = DateTimeOffset.UnixEpoch
		};

	private static ReportException CriterionLevelFinding() =>
		new()
		{
			ExceptionId = "EXC-CRITERION",
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			ControlId = null,
			Description = "Criterion controls not suitably designed"
		};

	private static ReportException ControlLevelFinding(string controlId) =>
		new()
		{
			ExceptionId = "EXC-CONTROL",
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			ControlId = controlId,
			Description = "Control operated ineffectively"
		};

	private static async Task<string> ExportAsync(ExportFormat format, params ReportException[] exceptions)
	{
		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			Title = "Control-identifier arm",
			ReportType = Soc2ReportType.TypeI,
			GeneratedAt = DateTimeOffset.UnixEpoch,
			PeriodStart = DateTimeOffset.UnixEpoch,
			PeriodEnd = DateTimeOffset.UnixEpoch,
			Opinion = AuditorOpinion.Unqualified,
			System = new SystemDescription
			{
				Name = "System",
				Description = "System under assessment",
				Services = ["Service"],
				Infrastructure = ["Host"],
				DataTypes = ["PII"]
			},
			ControlSections = [],
			CategoriesIncluded = [TrustServicesCategory.Security],
			Exceptions = [.. exceptions]
		};

		var exporter = new Soc2ReportExporter(NullLogger<Soc2ReportExporter>.Instance, TimeProvider.System);
		var result = await exporter.ExportAsync(report, format, null, CancellationToken.None)
			.ConfigureAwait(false);

		return Encoding.UTF8.GetString(result.Data);
	}
}
