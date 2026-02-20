// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportReceivedMessageShould
{
    [Fact]
    public void Have_Default_Id_As_Empty()
    {
        var msg = new TransportReceivedMessage();
        msg.Id.ShouldBe(string.Empty);
    }

    [Fact]
    public void Set_And_Get_All_Properties()
    {
        var now = DateTimeOffset.UtcNow;
        var lockExpires = now.AddMinutes(5);
        var msg = new TransportReceivedMessage
        {
            Id = "msg-1",
            Body = new byte[] { 1, 2 },
            ContentType = "application/json",
            MessageType = "OrderPlaced",
            CorrelationId = "corr-1",
            Subject = "orders",
            DeliveryCount = 3,
            EnqueuedAt = now,
            Source = "my-queue",
            PartitionKey = "pk-1",
            MessageGroupId = "group-1",
            LockExpiresAt = lockExpires,
        };

        msg.Id.ShouldBe("msg-1");
        msg.Body.ToArray().ShouldBe([1, 2]);
        msg.ContentType.ShouldBe("application/json");
        msg.MessageType.ShouldBe("OrderPlaced");
        msg.CorrelationId.ShouldBe("corr-1");
        msg.Subject.ShouldBe("orders");
        msg.DeliveryCount.ShouldBe(3);
        msg.EnqueuedAt.ShouldBe(now);
        msg.Source.ShouldBe("my-queue");
        msg.PartitionKey.ShouldBe("pk-1");
        msg.MessageGroupId.ShouldBe("group-1");
        msg.LockExpiresAt.ShouldBe(lockExpires);
    }

    [Fact]
    public void Have_Empty_Properties_By_Default()
    {
        var msg = new TransportReceivedMessage();
        msg.Properties.ShouldNotBeNull();
        msg.Properties.Count.ShouldBe(0);
    }

    [Fact]
    public void Support_Properties_Init()
    {
        var props = new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = "val" };
        var msg = new TransportReceivedMessage { Properties = props };
        msg.Properties["key"].ShouldBe("val");
    }

    [Fact]
    public void Have_Empty_ProviderData_By_Default()
    {
        var msg = new TransportReceivedMessage();
        msg.ProviderData.ShouldNotBeNull();
        msg.ProviderData.Count.ShouldBe(0);
    }

    [Fact]
    public void Support_ProviderData_Init()
    {
        var data = new Dictionary<string, object> { ["ReceiptHandle"] = "handle-123" };
        var msg = new TransportReceivedMessage { ProviderData = data };
        msg.ProviderData["ReceiptHandle"].ShouldBe("handle-123");
    }

    [Fact]
    public void Have_Default_Nullable_Properties_As_Null()
    {
        var msg = new TransportReceivedMessage();
        msg.ContentType.ShouldBeNull();
        msg.MessageType.ShouldBeNull();
        msg.CorrelationId.ShouldBeNull();
        msg.Subject.ShouldBeNull();
        msg.Source.ShouldBeNull();
        msg.PartitionKey.ShouldBeNull();
        msg.MessageGroupId.ShouldBeNull();
        msg.LockExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void Have_Default_DeliveryCount_As_Zero()
    {
        var msg = new TransportReceivedMessage();
        msg.DeliveryCount.ShouldBe(0);
    }
}
