// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationProgressAuditEventsShould
{
    [Fact]
    public void CreatePlaintextDataDetectedEventWithHighSeverity()
    {
        var evt = MigrationProgressAuditEvents.PlaintextDataDetected(
            "CustomerTable", 50, PlaintextSeverity.High, "scan", "corr-1");

        evt.Action.ShouldBe("PlaintextDataDetected");
        evt.EventType.ShouldBe(AuditEventType.Compliance);
        evt.Outcome.ShouldBe(AuditOutcome.Error);
        evt.ResourceType.ShouldBe("CustomerTable");
        evt.CorrelationId.ShouldBe("corr-1");
        evt.Metadata.ShouldContainKey("count");
        evt.Metadata["count"].ShouldBe("50");
        evt.Metadata["severity"].ShouldBe("High");
    }

    [Fact]
    public void CreatePlaintextDataDetectedEventWithLowSeverityAsPending()
    {
        var evt = MigrationProgressAuditEvents.PlaintextDataDetected(
            "LogTable", 10, PlaintextSeverity.Low, "audit");

        evt.Outcome.ShouldBe(AuditOutcome.Pending);
    }

    [Fact]
    public void CreateDataMigrationQueuedEvent()
    {
        var evt = MigrationProgressAuditEvents.DataMigrationQueued(
            "batch-1", "OrderTable", 1000, 1024 * 1024, "OldProvider", "NewProvider", "corr-2");

        evt.Action.ShouldBe("DataMigrationQueued");
        evt.EventType.ShouldBe(AuditEventType.Security);
        evt.Outcome.ShouldBe(AuditOutcome.Pending);
        evt.ResourceId.ShouldBe("batch-1");
        evt.Metadata.ShouldContainKey("recordCount");
        evt.Metadata["sourceProvider"].ShouldBe("OldProvider");
        evt.Metadata["targetProvider"].ShouldBe("NewProvider");
    }

    [Fact]
    public void CreateDataMigrationCompletedEventWithNoFailures()
    {
        var evt = MigrationProgressAuditEvents.DataMigrationCompleted(
            "batch-1", "Table", 100, 100, 0, 0, TimeSpan.FromMinutes(5));

        evt.Action.ShouldBe("DataMigrationCompleted");
        evt.Outcome.ShouldBe(AuditOutcome.Success);
    }

    [Fact]
    public void CreateDataMigrationCompletedEventWithPartialFailures()
    {
        var evt = MigrationProgressAuditEvents.DataMigrationCompleted(
            "batch-1", "Table", 100, 80, 20, 0, TimeSpan.FromMinutes(5));

        evt.Outcome.ShouldBe(AuditOutcome.Error);
    }

    [Fact]
    public void CreateDataMigrationCompletedEventWithTotalFailure()
    {
        var evt = MigrationProgressAuditEvents.DataMigrationCompleted(
            "batch-1", "Table", 100, 0, 100, 0, TimeSpan.FromMinutes(5));

        evt.Outcome.ShouldBe(AuditOutcome.Failure);
    }

    [Fact]
    public void CreateMigrationBatchProgressEvent()
    {
        var evt = MigrationProgressAuditEvents.MigrationBatchProgress(
            "batch-1", 3, 10, 300, 1000, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(7));

        evt.Action.ShouldBe("MigrationBatchProgress");
        evt.EventType.ShouldBe(AuditEventType.System);
        evt.ResourceType.ShouldBe("MigrationBatch");
        evt.Metadata.ShouldContainKey("progressPercentage");
        evt.Metadata["currentBatch"].ShouldBe("3");
    }
}
