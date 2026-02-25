// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

/// <summary>
/// Unit tests for <see cref="ComplianceStatus"/> and related types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Soc2")]
public sealed class ComplianceStatusShould : UnitTestBase
{
	[Fact]
	public void CreateValidComplianceStatus()
	{
		// Arrange
		var categoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
		{
			[TrustServicesCategory.Security] = new()
			{
				Category = TrustServicesCategory.Security,
				Level = ComplianceLevel.FullyCompliant,
				CompliancePercentage = 100,
				ActiveControls = 15,
				ControlsWithIssues = 0
			}
		};

		var criterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>
		{
			[TrustServicesCriterion.CC1_ControlEnvironment] = new()
			{
				Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
				IsMet = true,
				EffectivenessScore = 95,
				LastValidated = DateTimeOffset.UtcNow,
				EvidenceCount = 50
			}
		};

		// Act
		var status = new ComplianceStatus
		{
			OverallLevel = ComplianceLevel.FullyCompliant,
			CategoryStatuses = categoryStatuses,
			CriterionStatuses = criterionStatuses
		};

		// Assert
		status.OverallLevel.ShouldBe(ComplianceLevel.FullyCompliant);
		status.CategoryStatuses.Count.ShouldBe(1);
		status.CriterionStatuses.Count.ShouldBe(1);
		status.ActiveGaps.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(ComplianceLevel.FullyCompliant)]
	[InlineData(ComplianceLevel.SubstantiallyCompliant)]
	[InlineData(ComplianceLevel.PartiallyCompliant)]
	[InlineData(ComplianceLevel.NonCompliant)]
	[InlineData(ComplianceLevel.Unknown)]
	public void SupportAllComplianceLevels(ComplianceLevel level)
	{
		// Act
		var status = new ComplianceStatus
		{
			OverallLevel = level,
			CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>(),
			CriterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>()
		};

		// Assert
		status.OverallLevel.ShouldBe(level);
	}

	[Theory]
	[InlineData(ComplianceLevel.FullyCompliant, 0)]
	[InlineData(ComplianceLevel.SubstantiallyCompliant, 1)]
	[InlineData(ComplianceLevel.PartiallyCompliant, 2)]
	[InlineData(ComplianceLevel.NonCompliant, 3)]
	[InlineData(ComplianceLevel.Unknown, 4)]
	public void HaveCorrectComplianceLevelValues(ComplianceLevel level, int expectedValue)
	{
		// Assert
		((int)level).ShouldBe(expectedValue);
	}

	[Fact]
	public void CreateValidCategoryStatus()
	{
		// Act
		var categoryStatus = new CategoryStatus
		{
			Category = TrustServicesCategory.Availability,
			Level = ComplianceLevel.SubstantiallyCompliant,
			CompliancePercentage = 85,
			ActiveControls = 10,
			ControlsWithIssues = 2
		};

		// Assert
		categoryStatus.Category.ShouldBe(TrustServicesCategory.Availability);
		categoryStatus.Level.ShouldBe(ComplianceLevel.SubstantiallyCompliant);
		categoryStatus.CompliancePercentage.ShouldBe(85);
		categoryStatus.ActiveControls.ShouldBe(10);
		categoryStatus.ControlsWithIssues.ShouldBe(2);
	}

	[Fact]
	public void CreateValidCriterionStatus()
	{
		// Arrange
		var lastValidated = DateTimeOffset.UtcNow;

		// Act
		var criterionStatus = new CriterionStatus
		{
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			IsMet = false,
			EffectivenessScore = 70,
			LastValidated = lastValidated,
			EvidenceCount = 25,
			Gaps = ["Missing MFA enforcement", "Incomplete access review"]
		};

		// Assert
		criterionStatus.Criterion.ShouldBe(TrustServicesCriterion.CC6_LogicalAccess);
		criterionStatus.IsMet.ShouldBeFalse();
		criterionStatus.EffectivenessScore.ShouldBe(70);
		criterionStatus.LastValidated.ShouldBe(lastValidated);
		criterionStatus.EvidenceCount.ShouldBe(25);
		criterionStatus.Gaps.Count.ShouldBe(2);
	}

	[Fact]
	public void HaveEmptyGapsByDefault()
	{
		// Act
		var criterionStatus = new CriterionStatus
		{
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			IsMet = true,
			EffectivenessScore = 100,
			LastValidated = DateTimeOffset.UtcNow,
			EvidenceCount = 10
		};

		// Assert
		criterionStatus.Gaps.ShouldBeEmpty();
	}

	[Fact]
	public void CreateValidComplianceGap()
	{
		// Arrange
		var identifiedAt = DateTimeOffset.UtcNow;
		var targetDate = DateTimeOffset.UtcNow.AddMonths(1);

		// Act
		var gap = new ComplianceGap
		{
			GapId = "GAP-001",
			Criterion = TrustServicesCriterion.CC3_RiskAssessment,
			Description = "Risk assessment not performed quarterly",
			Severity = GapSeverity.High,
			Remediation = "Implement quarterly risk assessment process",
			IdentifiedAt = identifiedAt,
			TargetRemediationDate = targetDate
		};

		// Assert
		gap.GapId.ShouldBe("GAP-001");
		gap.Criterion.ShouldBe(TrustServicesCriterion.CC3_RiskAssessment);
		gap.Description.ShouldContain("risk assessment");
		gap.Severity.ShouldBe(GapSeverity.High);
		gap.Remediation.ShouldNotBeNullOrWhiteSpace();
		gap.IdentifiedAt.ShouldBe(identifiedAt);
		gap.TargetRemediationDate.ShouldBe(targetDate);
	}

	[Theory]
	[InlineData(GapSeverity.Low)]
	[InlineData(GapSeverity.Medium)]
	[InlineData(GapSeverity.High)]
	[InlineData(GapSeverity.Critical)]
	public void SupportAllGapSeverities(GapSeverity severity)
	{
		// Act
		var gap = new ComplianceGap
		{
			GapId = "TEST",
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			Description = "Test gap",
			Severity = severity,
			Remediation = "Fix it",
			IdentifiedAt = DateTimeOffset.UtcNow
		};

		// Assert
		gap.Severity.ShouldBe(severity);
	}

	[Theory]
	[InlineData(GapSeverity.Low, 0)]
	[InlineData(GapSeverity.Medium, 1)]
	[InlineData(GapSeverity.High, 2)]
	[InlineData(GapSeverity.Critical, 3)]
	public void HaveCorrectGapSeverityValues(GapSeverity severity, int expectedValue)
	{
		// Assert
		((int)severity).ShouldBe(expectedValue);
	}

	[Fact]
	public void AllowOptionalTargetRemediationDate()
	{
		// Act
		var gap = new ComplianceGap
		{
			GapId = "NO-TARGET",
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			Description = "Newly identified",
			Severity = GapSeverity.Low,
			Remediation = "TBD",
			IdentifiedAt = DateTimeOffset.UtcNow,
			TargetRemediationDate = null
		};

		// Assert
		gap.TargetRemediationDate.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var evaluatedAt = DateTimeOffset.UtcNow;
		var categoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>();
		var criterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>();

		var status1 = new ComplianceStatus
		{
			OverallLevel = ComplianceLevel.FullyCompliant,
			CategoryStatuses = categoryStatuses,
			CriterionStatuses = criterionStatuses,
			EvaluatedAt = evaluatedAt
		};

		var status2 = new ComplianceStatus
		{
			OverallLevel = ComplianceLevel.FullyCompliant,
			CategoryStatuses = categoryStatuses,
			CriterionStatuses = criterionStatuses,
			EvaluatedAt = evaluatedAt
		};

		// Assert
		status1.ShouldBe(status2);
	}
}
