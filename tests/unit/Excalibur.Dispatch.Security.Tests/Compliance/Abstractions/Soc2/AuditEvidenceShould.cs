// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

/// <summary>
/// Unit tests for <see cref="AuditEvidence"/> and related types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Soc2")]
public sealed class AuditEvidenceShould : UnitTestBase
{
	[Fact]
	public void CreateValidAuditEvidence()
	{
		// Arrange
		var periodStart = DateTimeOffset.UtcNow.AddMonths(-6);
		var periodEnd = DateTimeOffset.UtcNow;
		var items = new List<EvidenceItem>
		{
			new()
			{
				EvidenceId = "EVD-001",
				Type = EvidenceType.AuditLog,
				Description = "Access control audit logs",
				Source = "Azure AD",
				CollectedAt = DateTimeOffset.UtcNow
			}
		};
		var summary = new EvidenceSummary
		{
			TotalItems = 1,
			ByType = new Dictionary<EvidenceType, int> { [EvidenceType.AuditLog] = 1 },
			AuditLogEntries = 10000,
			ConfigurationSnapshots = 0,
			TestResults = 0
		};

		// Act
		var evidence = new AuditEvidence
		{
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			Items = items,
			Summary = summary,
			ChainOfCustodyHash = "sha256:abc123def456"
		};

		// Assert
		evidence.Criterion.ShouldBe(TrustServicesCriterion.CC6_LogicalAccess);
		evidence.PeriodStart.ShouldBe(periodStart);
		evidence.PeriodEnd.ShouldBe(periodEnd);
		evidence.Items.Count.ShouldBe(1);
		evidence.Summary.TotalItems.ShouldBe(1);
		evidence.ChainOfCustodyHash.ShouldBe("sha256:abc123def456");
	}

	[Fact]
	public void CreateValidEvidenceItem()
	{
		// Arrange
		var collectedAt = DateTimeOffset.UtcNow;

		// Act
		var item = new EvidenceItem
		{
			EvidenceId = "EVD-100",
			Type = EvidenceType.Configuration,
			Description = "Azure AD security configuration",
			Source = "Azure Portal Export",
			CollectedAt = collectedAt,
			DataReference = "/evidence/2026/01/azure-ad-config.json"
		};

		// Assert
		item.EvidenceId.ShouldBe("EVD-100");
		item.Type.ShouldBe(EvidenceType.Configuration);
		item.Description.ShouldBe("Azure AD security configuration");
		item.Source.ShouldBe("Azure Portal Export");
		item.CollectedAt.ShouldBe(collectedAt);
		item.DataReference.ShouldBe("/evidence/2026/01/azure-ad-config.json");
	}

	[Theory]
	[InlineData(EvidenceType.Configuration)]
	[InlineData(EvidenceType.AuditLog)]
	[InlineData(EvidenceType.Metrics)]
	[InlineData(EvidenceType.Policy)]
	[InlineData(EvidenceType.TestResult)]
	[InlineData(EvidenceType.Acknowledgment)]
	[InlineData(EvidenceType.Attestation)]
	public void SupportAllEvidenceTypes(EvidenceType type)
	{
		// Act
		var item = new EvidenceItem
		{
			EvidenceId = "TEST",
			Type = type,
			Description = "Test",
			Source = "Test",
			CollectedAt = DateTimeOffset.UtcNow
		};

		// Assert
		item.Type.ShouldBe(type);
	}

	[Theory]
	[InlineData(EvidenceType.Configuration, 0)]
	[InlineData(EvidenceType.AuditLog, 1)]
	[InlineData(EvidenceType.Metrics, 2)]
	[InlineData(EvidenceType.Policy, 3)]
	[InlineData(EvidenceType.TestResult, 4)]
	[InlineData(EvidenceType.Acknowledgment, 5)]
	[InlineData(EvidenceType.Attestation, 6)]
	public void HaveCorrectEvidenceTypeValues(EvidenceType type, int expectedValue)
	{
		// Assert
		((int)type).ShouldBe(expectedValue);
	}

	[Fact]
	public void AllowOptionalDataReference()
	{
		// Act
		var item = new EvidenceItem
		{
			EvidenceId = "NO-REF",
			Type = EvidenceType.Acknowledgment,
			Description = "User acknowledgment",
			Source = "Training System",
			CollectedAt = DateTimeOffset.UtcNow,
			DataReference = null
		};

		// Assert
		item.DataReference.ShouldBeNull();
	}

	[Fact]
	public void CreateValidEvidenceSummary()
	{
		// Arrange
		var byType = new Dictionary<EvidenceType, int>
		{
			[EvidenceType.AuditLog] = 5,
			[EvidenceType.Configuration] = 3,
			[EvidenceType.TestResult] = 10,
			[EvidenceType.Policy] = 2
		};

		// Act
		var summary = new EvidenceSummary
		{
			TotalItems = 20,
			ByType = byType,
			AuditLogEntries = 50000,
			ConfigurationSnapshots = 15,
			TestResults = 100
		};

		// Assert
		summary.TotalItems.ShouldBe(20);
		summary.ByType.Count.ShouldBe(4);
		summary.ByType[EvidenceType.AuditLog].ShouldBe(5);
		summary.AuditLogEntries.ShouldBe(50000);
		summary.ConfigurationSnapshots.ShouldBe(15);
		summary.TestResults.ShouldBe(100);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var periodStart = DateTimeOffset.UtcNow;
		var periodEnd = DateTimeOffset.UtcNow;
		var items = new List<EvidenceItem>();
		var summary = new EvidenceSummary
		{
			TotalItems = 0,
			ByType = new Dictionary<EvidenceType, int>(),
			AuditLogEntries = 0,
			ConfigurationSnapshots = 0,
			TestResults = 0
		};

		var evidence1 = new AuditEvidence
		{
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			Items = items,
			Summary = summary,
			ChainOfCustodyHash = "hash1"
		};

		var evidence2 = new AuditEvidence
		{
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			Items = items,
			Summary = summary,
			ChainOfCustodyHash = "hash1"
		};

		// Assert
		evidence1.ShouldBe(evidence2);
	}
}
