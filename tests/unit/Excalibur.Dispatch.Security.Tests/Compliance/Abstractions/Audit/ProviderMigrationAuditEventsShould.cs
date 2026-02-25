// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ProviderMigrationAuditEventsShould
{
    [Fact]
    public void CreateDataReEncryptedEvent()
    {
        var evt = ProviderMigrationAuditEvents.DataReEncrypted(
            "ProviderA", "ProviderB", "AES-128", "AES-256",
            "key-old", "key-new", "res-1", "OrderTable", "system", "corr-1");

        evt.Action.ShouldBe("DataReEncrypted");
        evt.EventType.ShouldBe(AuditEventType.Security);
        evt.Outcome.ShouldBe(AuditOutcome.Success);
        evt.ResourceId.ShouldBe("res-1");
        evt.Metadata["sourceProvider"].ShouldBe("ProviderA");
        evt.Metadata["targetProvider"].ShouldBe("ProviderB");
        evt.Metadata["sourceKeyId"].ShouldBe("key-old");
        evt.Metadata["targetKeyId"].ShouldBe("key-new");
    }

    [Fact]
    public void CreateProviderMigrationCompletedEventWithNoFailures()
    {
        var evt = ProviderMigrationAuditEvents.ProviderMigrationCompleted(
            "OldProv", "NewProv", 100, 0, 5, TimeSpan.FromMinutes(10), "system");

        evt.Action.ShouldBe("ProviderMigrationCompleted");
        evt.Outcome.ShouldBe(AuditOutcome.Success);
        evt.Metadata["migratedCount"].ShouldBe("100");
        evt.Metadata["skippedCount"].ShouldBe("5");
    }

    [Fact]
    public void CreateProviderMigrationCompletedEventWithFailures()
    {
        var evt = ProviderMigrationAuditEvents.ProviderMigrationCompleted(
            "OldProv", "NewProv", 80, 20, 0, TimeSpan.FromMinutes(10), "system");

        evt.Outcome.ShouldBe(AuditOutcome.Failure);
    }

    [Fact]
    public void CreateDecryptionMigrationCompletedEventWithNoFailures()
    {
        var evt = ProviderMigrationAuditEvents.DecryptionMigrationCompleted(
            "Provider1", 500, 500, 0, TimeSpan.FromMinutes(15), "admin", "decommission");

        evt.Action.ShouldBe("DecryptionMigrationCompleted");
        evt.Outcome.ShouldBe(AuditOutcome.Success);
        evt.Reason.ShouldBe("decommission");
    }

    [Fact]
    public void CreateDecryptionMigrationCompletedEventWithFailures()
    {
        var evt = ProviderMigrationAuditEvents.DecryptionMigrationCompleted(
            "Provider1", 500, 490, 10, TimeSpan.FromMinutes(15), "admin");

        evt.Outcome.ShouldBe(AuditOutcome.Failure);
    }
}
