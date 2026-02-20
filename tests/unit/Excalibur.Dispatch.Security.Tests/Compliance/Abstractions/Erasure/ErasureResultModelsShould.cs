// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureResultModelsShould
{
    [Fact]
    public void CreateScheduledResult()
    {
        var id = Guid.NewGuid();
        var scheduledTime = DateTimeOffset.UtcNow.AddHours(72);

        var result = ErasureResult.Scheduled(id, scheduledTime);

        result.RequestId.ShouldBe(id);
        result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
        result.ScheduledExecutionTime.ShouldBe(scheduledTime);
        result.EstimatedCompletionTime.ShouldNotBeNull();
    }

    [Fact]
    public void CreateScheduledResultWithInventory()
    {
        var id = Guid.NewGuid();
        var inventory = new DataInventorySummary
        {
            EncryptedFieldCount = 5,
            KeyCount = 2
        };

        var result = ErasureResult.Scheduled(id, DateTimeOffset.UtcNow, inventory);

        result.InventorySummary.ShouldBe(inventory);
    }

    [Fact]
    public void CreateBlockedResult()
    {
        var id = Guid.NewGuid();
        var hold = new LegalHoldInfo
        {
            HoldId = Guid.NewGuid(),
            Basis = LegalHoldBasis.LitigationHold,
            CaseReference = "CASE-456",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = ErasureResult.Blocked(id, hold);

        result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
        result.BlockingHold.ShouldBe(hold);
        result.Message.ShouldContain("CASE-456");
    }

    [Fact]
    public void CreateFailedResult()
    {
        var id = Guid.NewGuid();
        var result = ErasureResult.Failed(id, "Something went wrong");

        result.Status.ShouldBe(ErasureRequestStatus.Failed);
        result.Message.ShouldBe("Something went wrong");
    }

    [Fact]
    public void CreateErasureRequestWithDefaults()
    {
        var request = new ErasureRequest
        {
            DataSubjectId = "user-123",
            IdType = DataSubjectIdType.UserId,
            LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
            RequestedBy = "admin"
        };

        request.RequestId.ShouldNotBe(Guid.Empty);
        request.Scope.ShouldBe(ErasureScope.User);
        request.TenantId.ShouldBeNull();
        request.GracePeriodOverride.ShouldBeNull();
        request.DataCategories.ShouldBeNull();
    }

    [Fact]
    public void CreateDataInventorySummaryWithDefaults()
    {
        var summary = new DataInventorySummary();

        summary.EncryptedFieldCount.ShouldBe(0);
        summary.KeyCount.ShouldBe(0);
        summary.DataCategories.ShouldBeEmpty();
        summary.AffectedTables.ShouldBeEmpty();
        summary.EstimatedDataSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void CreateLegalHoldInfo()
    {
        var hold = new LegalHoldInfo
        {
            HoldId = Guid.NewGuid(),
            Basis = LegalHoldBasis.RegulatoryInvestigation,
            CaseReference = "REG-2025-001",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
        };

        hold.Basis.ShouldBe(LegalHoldBasis.RegulatoryInvestigation);
        hold.ExpiresAt.ShouldNotBeNull();
    }
}
