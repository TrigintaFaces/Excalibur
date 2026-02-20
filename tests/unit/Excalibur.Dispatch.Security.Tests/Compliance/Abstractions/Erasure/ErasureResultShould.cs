// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureResult"/> and related types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Erasure")]
public sealed class ErasureResultShould : UnitTestBase
{
	[Fact]
	public void CreateScheduledResultWithFactoryMethod()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(24);
		var inventory = new DataInventorySummary
		{
			EncryptedFieldCount = 10,
			KeyCount = 2,
			DataCategories = ["Personal", "Financial"],
			AffectedTables = ["Users", "Accounts"],
			EstimatedDataSizeBytes = 1024
		};

		// Act
		var result = ErasureResult.Scheduled(requestId, scheduledTime, inventory);

		// Assert
		result.RequestId.ShouldBe(requestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
		result.ScheduledExecutionTime.ShouldBe(scheduledTime);
		result.InventorySummary.ShouldBe(inventory);
		result.EstimatedCompletionTime.ShouldBe(scheduledTime.AddMinutes(5));
		result.BlockingHold.ShouldBeNull();
		result.Message.ShouldBeNull();
	}

	[Fact]
	public void CreateBlockedResultWithFactoryMethod()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var hold = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-2026-001",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(365)
		};

		// Act
		var result = ErasureResult.Blocked(requestId, hold);

		// Assert
		result.RequestId.ShouldBe(requestId);
		result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
		result.BlockingHold.ShouldBe(hold);
		result.Message.ShouldContain("CASE-2026-001");
		result.ScheduledExecutionTime.ShouldBeNull();
		result.InventorySummary.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResultWithFactoryMethod()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var errorMessage = "Database connection failed";

		// Act
		var result = ErasureResult.Failed(requestId, errorMessage);

		// Assert
		result.RequestId.ShouldBe(requestId);
		result.Status.ShouldBe(ErasureRequestStatus.Failed);
		result.Message.ShouldBe(errorMessage);
		result.ScheduledExecutionTime.ShouldBeNull();
		result.InventorySummary.ShouldBeNull();
		result.BlockingHold.ShouldBeNull();
	}

	[Theory]
	[InlineData(ErasureRequestStatus.Pending)]
	[InlineData(ErasureRequestStatus.Scheduled)]
	[InlineData(ErasureRequestStatus.InProgress)]
	[InlineData(ErasureRequestStatus.Completed)]
	[InlineData(ErasureRequestStatus.BlockedByLegalHold)]
	[InlineData(ErasureRequestStatus.Cancelled)]
	[InlineData(ErasureRequestStatus.Failed)]
	[InlineData(ErasureRequestStatus.PartiallyCompleted)]
	public void SupportAllErasureRequestStatuses(ErasureRequestStatus status)
	{
		// Act
		var result = new ErasureResult
		{
			RequestId = Guid.NewGuid(),
			Status = status
		};

		// Assert
		result.Status.ShouldBe(status);
	}

	[Theory]
	[InlineData(LegalHoldBasis.FreedomOfExpression)]
	[InlineData(LegalHoldBasis.LegalObligation)]
	[InlineData(LegalHoldBasis.PublicInterestHealth)]
	[InlineData(LegalHoldBasis.ArchivingResearchStatistics)]
	[InlineData(LegalHoldBasis.LegalClaims)]
	[InlineData(LegalHoldBasis.LitigationHold)]
	[InlineData(LegalHoldBasis.RegulatoryInvestigation)]
	public void SupportAllLegalHoldBases(LegalHoldBasis basis)
	{
		// Act
		var hold = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = basis,
			CaseReference = "TEST-001",
			CreatedAt = DateTimeOffset.UtcNow
		};

		// Assert
		hold.Basis.ShouldBe(basis);
	}

	[Fact]
	public void CreateDataInventorySummaryWithDefaults()
	{
		// Act
		var summary = new DataInventorySummary();

		// Assert
		summary.EncryptedFieldCount.ShouldBe(0);
		summary.KeyCount.ShouldBe(0);
		summary.DataCategories.ShouldBeEmpty();
		summary.AffectedTables.ShouldBeEmpty();
		summary.EstimatedDataSizeBytes.ShouldBe(0);
	}

	[Fact]
	public void CreateFullyPopulatedDataInventorySummary()
	{
		// Act
		var summary = new DataInventorySummary
		{
			EncryptedFieldCount = 150,
			KeyCount = 5,
			DataCategories = ["PII", "Financial", "Health"],
			AffectedTables = ["Users", "Orders", "Payments", "MedicalRecords"],
			EstimatedDataSizeBytes = 1024 * 1024 * 100 // 100 MB
		};

		// Assert
		summary.EncryptedFieldCount.ShouldBe(150);
		summary.KeyCount.ShouldBe(5);
		summary.DataCategories.Count.ShouldBe(3);
		summary.AffectedTables.Count.ShouldBe(4);
		summary.EstimatedDataSizeBytes.ShouldBe(104857600);
	}

	[Fact]
	public void CreateLegalHoldInfoWithAllProperties()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var createdAt = DateTimeOffset.UtcNow.AddMonths(-6);
		var expiresAt = DateTimeOffset.UtcNow.AddMonths(6);

		// Act
		var hold = new LegalHoldInfo
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.RegulatoryInvestigation,
			CaseReference = "SEC-INV-2026-0042",
			CreatedAt = createdAt,
			ExpiresAt = expiresAt
		};

		// Assert
		hold.HoldId.ShouldBe(holdId);
		hold.Basis.ShouldBe(LegalHoldBasis.RegulatoryInvestigation);
		hold.CaseReference.ShouldBe("SEC-INV-2026-0042");
		hold.CreatedAt.ShouldBe(createdAt);
		hold.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void AllowIndefiniteLegalHold()
	{
		// Act
		var hold = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LegalClaims,
			CaseReference = "INDEFINITE-HOLD",
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = null
		};

		// Assert
		hold.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEqualityForErasureResult()
	{
		// Arrange
		var requestId = Guid.NewGuid();

		var result1 = new ErasureResult
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Pending
		};

		var result2 = new ErasureResult
		{
			RequestId = requestId,
			Status = ErasureRequestStatus.Pending
		};

		// Assert
		result1.ShouldBe(result2);
	}
}
