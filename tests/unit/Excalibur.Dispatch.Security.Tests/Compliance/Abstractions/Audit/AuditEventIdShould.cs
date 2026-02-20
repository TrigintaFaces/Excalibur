// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditEventIdShould
{
    [Fact]
    public void CreateWithRequiredProperties()
    {
        var id = new AuditEventId
        {
            EventId = "evt-1",
            EventHash = "hash-abc",
            SequenceNumber = 42,
            RecordedAt = DateTimeOffset.UtcNow
        };

        id.EventId.ShouldBe("evt-1");
        id.EventHash.ShouldBe("hash-abc");
        id.SequenceNumber.ShouldBe(42);
    }

    [Fact]
    public void SupportValueEquality()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new AuditEventId { EventId = "e1", EventHash = "h1", SequenceNumber = 1, RecordedAt = now };
        var b = new AuditEventId { EventId = "e1", EventHash = "h1", SequenceNumber = 1, RecordedAt = now };

        a.ShouldBe(b);
    }

    [Fact]
    public void BeReadonlyRecordStruct()
    {
        typeof(AuditEventId).IsValueType.ShouldBeTrue();
    }
}
