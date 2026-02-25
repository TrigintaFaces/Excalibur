// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

/// <summary>
/// Unit tests for SOC2 compliance types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Soc2")]
public sealed class Soc2TypesShould : UnitTestBase
{
	[Fact]
	public void CreateValidControlValidationResult()
	{
		// Act
		var validation = new ControlValidationResult
		{
			ControlId = "CC6.1",
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 95,
			ConfigurationIssues = ["Minor issue 1"],
			Evidence = []
		};

		// Assert
		validation.ControlId.ShouldBe("CC6.1");
		validation.IsConfigured.ShouldBeTrue();
		validation.IsEffective.ShouldBeTrue();
		validation.EffectivenessScore.ShouldBe(95);
		validation.ConfigurationIssues.Count.ShouldBe(1);
	}

	[Fact]
	public void CreateControlValidationResultWithDefaultTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var validation = new ControlValidationResult
		{
			ControlId = "TEST",
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 100
		};

		var after = DateTimeOffset.UtcNow;

		// Assert
		validation.ValidatedAt.ShouldBeGreaterThanOrEqualTo(before);
		validation.ValidatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void CreateValidControlTestParameters()
	{
		// Act
		var parameters = new ControlTestParameters
		{
			SampleSize = 50,
			PeriodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			PeriodEnd = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
			IncludeDetailedEvidence = true
		};

		// Assert
		parameters.SampleSize.ShouldBe(50);
		parameters.IncludeDetailedEvidence.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultSampleSizeOf25()
	{
		// Act
		var parameters = new ControlTestParameters();

		// Assert
		parameters.SampleSize.ShouldBe(25);
		parameters.IncludeDetailedEvidence.ShouldBeTrue();
	}

	[Theory]
	[InlineData(Soc2ReportType.TypeI)]
	[InlineData(Soc2ReportType.TypeII)]
	public void SupportBothSoc2ReportTypes(Soc2ReportType reportType)
	{
		// Assert
		Enum.IsDefined(reportType).ShouldBeTrue();
	}

	[Theory]
	[InlineData(ControlType.Preventive)]
	[InlineData(ControlType.Detective)]
	[InlineData(ControlType.Corrective)]
	public void SupportAllControlTypes(ControlType controlType)
	{
		// Assert
		Enum.IsDefined(controlType).ShouldBeTrue();
	}

	[Theory]
	[InlineData(ControlFrequency.Continuous)]
	[InlineData(ControlFrequency.PerTransaction)]
	[InlineData(ControlFrequency.Daily)]
	[InlineData(ControlFrequency.Weekly)]
	[InlineData(ControlFrequency.Monthly)]
	[InlineData(ControlFrequency.Quarterly)]
	[InlineData(ControlFrequency.Annually)]
	[InlineData(ControlFrequency.OnDemand)]
	public void SupportAllControlFrequencies(ControlFrequency frequency)
	{
		// Assert
		Enum.IsDefined(frequency).ShouldBeTrue();
	}

	[Theory]
	[InlineData(TestOutcome.NoExceptions)]
	[InlineData(TestOutcome.MinorExceptions)]
	[InlineData(TestOutcome.SignificantExceptions)]
	[InlineData(TestOutcome.ControlFailure)]
	public void SupportAllTestOutcomes(TestOutcome outcome)
	{
		// Assert
		Enum.IsDefined(outcome).ShouldBeTrue();
	}

	[Theory]
	[InlineData(AuditorOpinion.Unqualified)]
	[InlineData(AuditorOpinion.Qualified)]
	[InlineData(AuditorOpinion.Adverse)]
	[InlineData(AuditorOpinion.Disclaimer)]
	public void SupportAllAuditorOpinions(AuditorOpinion opinion)
	{
		// Assert
		Enum.IsDefined(opinion).ShouldBeTrue();
	}

	[Fact]
	public void CreateValidControlTestResult()
	{
		// Arrange
		var parameters = new ControlTestParameters
		{
			SampleSize = 25,
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-1),
			PeriodEnd = DateTimeOffset.UtcNow
		};

		// Act
		var result = new ControlTestResult
		{
			ControlId = "CC6.1",
			Parameters = parameters,
			ItemsTested = 25,
			ExceptionsFound = 0,
			Outcome = TestOutcome.NoExceptions
		};

		// Assert
		result.ControlId.ShouldBe("CC6.1");
		result.ItemsTested.ShouldBe(25);
		result.ExceptionsFound.ShouldBe(0);
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.Exceptions.ShouldBeEmpty();
		result.Evidence.ShouldBeEmpty();
	}

	[Fact]
	public void CreateValidTestException()
	{
		// Act
		var exception = new TestException
		{
			ItemId = "TXN-001",
			Description = "Transaction missing approval signature",
			Severity = GapSeverity.Medium,
			OccurredAt = DateTimeOffset.UtcNow
		};

		// Assert
		exception.ItemId.ShouldBe("TXN-001");
		exception.Description.ShouldContain("approval signature");
		exception.Severity.ShouldBe(GapSeverity.Medium);
	}

	[Fact]
	public void CreateValidControlDescription()
	{
		// Act
		var control = new ControlDescription
		{
			ControlId = "CC6.1",
			Name = "Logical Access Controls",
			Description = "Access to systems is restricted based on role",
			Implementation = "RBAC implemented via Azure AD",
			Type = ControlType.Preventive,
			Frequency = ControlFrequency.Continuous
		};

		// Assert
		control.ControlId.ShouldBe("CC6.1");
		control.Name.ShouldBe("Logical Access Controls");
		control.Type.ShouldBe(ControlType.Preventive);
		control.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void CreateValidSystemDescription()
	{
		// Act
		var system = new SystemDescription
		{
			Name = "Excalibur Platform",
			Description = "Enterprise messaging and compliance platform",
			Services = ["Message Processing", "Encryption", "Audit Logging"],
			Infrastructure = ["Azure", "SQL Server", "Redis"],
			DataTypes = ["PII", "Financial Data"]
		};

		// Assert
		system.Name.ShouldBe("Excalibur Platform");
		system.Services.Count.ShouldBe(3);
		system.Infrastructure.Count.ShouldBe(3);
		system.DataTypes.Count.ShouldBe(2);
		system.ThirdParties.ShouldBeEmpty();
	}

	[Fact]
	public void CreateValidReportException()
	{
		// Act
		var exception = new ReportException
		{
			ExceptionId = "EXC-001",
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			ControlId = "CC6.1",
			Description = "Access review not performed quarterly",
			ManagementResponse = "Will implement quarterly reviews",
			RemediationPlan = "Schedule automated reminders"
		};

		// Assert
		exception.ExceptionId.ShouldBe("EXC-001");
		exception.ControlId.ShouldBe("CC6.1");
		_ = exception.ManagementResponse.ShouldNotBeNull();
		_ = exception.RemediationPlan.ShouldNotBeNull();
	}

	[Fact]
	public void CreateValidTestResult()
	{
		// Act
		var result = new TestResult
		{
			ControlId = "CC6.1",
			TestProcedure = "Review access logs for unauthorized access attempts",
			SampleSize = 25,
			ExceptionsFound = 0,
			Outcome = TestOutcome.NoExceptions,
			Notes = "All samples passed validation"
		};

		// Assert
		result.ControlId.ShouldBe("CC6.1");
		result.SampleSize.ShouldBe(25);
		result.ExceptionsFound.ShouldBe(0);
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.Notes.ShouldBe("All samples passed validation");
	}
}
