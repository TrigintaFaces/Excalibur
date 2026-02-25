// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionAuditEventsShould
{
    [Fact]
    public void CreateFailoverInitiatedEvent()
    {
        var evt = MultiRegionAuditEvents.FailoverInitiated(
            "westeurope", "northeurope", "Health check failed", true, "System", "corr-1");

        evt.Action.ShouldBe("FailoverInitiated");
        evt.EventType.ShouldBe(AuditEventType.Security);
        evt.Outcome.ShouldBe(AuditOutcome.Success);
        evt.ActorType.ShouldBe("System");
        evt.Metadata["isAutomatic"].ShouldBe("True");
        evt.Metadata["sourceRegion"].ShouldBe("westeurope");
    }

    [Fact]
    public void CreateManualFailoverWithUserActorType()
    {
        var evt = MultiRegionAuditEvents.FailoverInitiated(
            "us-east-1", "eu-west-1", "Planned maintenance", false, "admin@example.com");

        evt.ActorType.ShouldBe("User");
        evt.ActorId.ShouldBe("admin@example.com");
    }

    [Fact]
    public void CreateFailbackCompletedEvent()
    {
        var evt = MultiRegionAuditEvents.FailbackCompleted(
            "northeurope", "westeurope", "Primary restored", "admin", 42, "corr-2");

        evt.Action.ShouldBe("FailbackCompleted");
        evt.Metadata["keysSynchronized"].ShouldBe("42");
        evt.CorrelationId.ShouldBe("corr-2");
    }

    [Fact]
    public void CreateRpoThresholdBreachedEvent()
    {
        var evt = MultiRegionAuditEvents.RpoThresholdBreached(
            TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(15), "westeurope", 5);

        evt.Action.ShouldBe("RpoThresholdBreached");
        evt.EventType.ShouldBe(AuditEventType.Compliance);
        evt.Outcome.ShouldBe(AuditOutcome.Pending);
        evt.Reason.ShouldContain("20.0m");
        evt.Reason.ShouldContain("15.0m");
    }

    [Fact]
    public void CreateReplicationSyncCompletedEvent()
    {
        var evt = MultiRegionAuditEvents.ReplicationSyncCompleted(
            "westeurope", "northeurope", 10, TimeSpan.FromSeconds(30));

        evt.Action.ShouldBe("ReplicationSyncCompleted");
        evt.Outcome.ShouldBe(AuditOutcome.Success);
        evt.Metadata["keyCount"].ShouldBe("10");
    }

    [Fact]
    public void CreateRegionHealthChangedEventForUnhealthy()
    {
        var evt = MultiRegionAuditEvents.RegionHealthChanged(
            "westeurope", true, false, 3, "Connection timeout");

        evt.Action.ShouldBe("RegionHealthChanged");
        evt.Outcome.ShouldBe(AuditOutcome.Error);
        evt.Reason.ShouldBe("Healthy->Unhealthy");
        evt.Metadata["consecutiveFailures"].ShouldBe("3");
    }

    [Fact]
    public void CreateRegionHealthChangedEventForRecovery()
    {
        var evt = MultiRegionAuditEvents.RegionHealthChanged(
            "westeurope", false, true, 0);

        evt.Outcome.ShouldBe(AuditOutcome.Success);
        evt.Reason.ShouldBe("Unhealthy->Healthy");
    }
}
