// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportMessageShould
{
    [Fact]
    public void Have_Generated_Id_By_Default()
    {
        var msg = new TransportMessage();
        msg.Id.ShouldNotBeNullOrEmpty();
        Guid.TryParse(msg.Id, out _).ShouldBeTrue();
    }

    [Fact]
    public void Have_Unique_Ids_Across_Instances()
    {
        var msg1 = new TransportMessage();
        var msg2 = new TransportMessage();
        msg1.Id.ShouldNotBe(msg2.Id);
    }

    [Fact]
    public void Have_Default_Body_As_Empty()
    {
        var msg = new TransportMessage();
        msg.Body.Length.ShouldBe(0);
    }

    [Fact]
    public void Set_And_Get_Body()
    {
        var body = new byte[] { 1, 2, 3 };
        var msg = new TransportMessage { Body = body };
        msg.Body.ToArray().ShouldBe(body);
    }

    [Fact]
    public void Set_And_Get_ContentType()
    {
        var msg = new TransportMessage { ContentType = "application/json" };
        msg.ContentType.ShouldBe("application/json");
    }

    [Fact]
    public void Set_And_Get_MessageType()
    {
        var msg = new TransportMessage { MessageType = "OrderCreated" };
        msg.MessageType.ShouldBe("OrderCreated");
    }

    [Fact]
    public void Set_And_Get_CorrelationId()
    {
        var msg = new TransportMessage { CorrelationId = "corr-123" };
        msg.CorrelationId.ShouldBe("corr-123");
    }

    [Fact]
    public void Set_And_Get_Subject()
    {
        var msg = new TransportMessage { Subject = "order.created" };
        msg.Subject.ShouldBe("order.created");
    }

    [Fact]
    public void Set_And_Get_TimeToLive()
    {
        var ttl = TimeSpan.FromMinutes(5);
        var msg = new TransportMessage { TimeToLive = ttl };
        msg.TimeToLive.ShouldBe(ttl);
    }

    [Fact]
    public void Have_Default_TimeToLive_As_Null()
    {
        var msg = new TransportMessage();
        msg.TimeToLive.ShouldBeNull();
    }

    [Fact]
    public void Have_CreatedAt_Set_To_Approximate_Now()
    {
        var before = DateTimeOffset.UtcNow;
        var msg = new TransportMessage();
        var after = DateTimeOffset.UtcNow;

        msg.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        msg.CreatedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Have_Empty_Properties_By_Default()
    {
        var msg = new TransportMessage();
        msg.Properties.ShouldNotBeNull();
        msg.Properties.Count.ShouldBe(0);
    }

    [Fact]
    public void Support_Adding_Properties()
    {
        var msg = new TransportMessage();
        msg.Properties["key1"] = "value1";
        msg.Properties["key1"].ShouldBe("value1");
    }

    [Fact]
    public void Report_HasProperties_False_When_No_Properties()
    {
        var msg = new TransportMessage();
        msg.HasProperties.ShouldBeFalse();
    }

    [Fact]
    public void Report_HasProperties_True_When_Properties_Exist()
    {
        var msg = new TransportMessage();
        msg.Properties["key"] = "val";
        msg.HasProperties.ShouldBeTrue();
    }

    [Fact]
    public void Support_Properties_Init()
    {
        var props = new Dictionary<string, object> { ["a"] = 1 };
        var msg = new TransportMessage { Properties = props };
        msg.Properties.ShouldBeSameAs(props);
        msg.HasProperties.ShouldBeTrue();
    }

    [Fact]
    public void Create_FromBytes()
    {
        var body = new byte[] { 10, 20, 30 };
        var msg = TransportMessage.FromBytes(body);

        msg.Body.ToArray().ShouldBe(body);
        msg.ContentType.ShouldBeNull();
    }

    [Fact]
    public void Create_FromString()
    {
        var msg = TransportMessage.FromString("hello world");

        Encoding.UTF8.GetString(msg.Body.Span).ShouldBe("hello world");
        msg.ContentType.ShouldBe("text/plain");
    }

    [Fact]
    public void Create_FromString_With_Utf8_Encoding()
    {
        var msg = TransportMessage.FromString("héllo wörld");

        Encoding.UTF8.GetString(msg.Body.Span).ShouldBe("héllo wörld");
    }
}
