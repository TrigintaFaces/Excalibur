using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeStructShould
{
	private static readonly byte[] TestBody = [1, 2, 3, 4];
	private static readonly long TestTicks = DateTimeOffset.UtcNow.Ticks;

	[Fact]
	public void CreateWithRequiredParameters()
	{
		var envelope = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);

		envelope.MessageId.ShouldBe("msg-1");
		envelope.Body.Length.ShouldBe(4);
		envelope.TimestampTicks.ShouldBe(TestTicks);
		envelope.Headers.ShouldBeNull();
		envelope.CorrelationId.ShouldBeNull();
		envelope.MessageType.ShouldBeNull();
		envelope.Priority.ShouldBe((byte)0);
		envelope.TimeToLiveSeconds.ShouldBe(0);
	}

	[Fact]
	public void CreateWithAllParameters()
	{
		var headers = new Dictionary<string, string> { ["key"] = "val" };

		var envelope = new MessageEnvelopeStruct(
			"msg-2", TestBody, TestTicks, headers, "corr-1", "OrderCreated", 5, 300);

		envelope.MessageId.ShouldBe("msg-2");
		envelope.CorrelationId.ShouldBe("corr-1");
		envelope.MessageType.ShouldBe("OrderCreated");
		envelope.Priority.ShouldBe((byte)5);
		envelope.TimeToLiveSeconds.ShouldBe(300);
		envelope.Headers.ShouldNotBeNull();
		envelope.Headers!["key"].ShouldBe("val");
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		Should.Throw<ArgumentNullException>(() => new MessageEnvelopeStruct(null!, TestBody, TestTicks));
	}

	[Fact]
	public void ComputeTimestampFromTicks()
	{
		var now = DateTime.UtcNow;
		var envelope = new MessageEnvelopeStruct("msg-1", TestBody, now.Ticks);

		envelope.Timestamp.ShouldBe(now);
	}

	[Fact]
	public void ComputeTimeToLiveFromSeconds()
	{
		var envelope = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, timeToLiveSeconds: 60);

		envelope.TimeToLive.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void WithMessageId_ReturnsNewInstance()
	{
		var original = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, correlationId: "corr-1");
		var modified = original.WithMessageId("msg-2");

		modified.MessageId.ShouldBe("msg-2");
		modified.CorrelationId.ShouldBe("corr-1");
		original.MessageId.ShouldBe("msg-1");
	}

	[Fact]
	public void WithBody_ReturnsNewInstance()
	{
		var original = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);
		var newBody = new byte[] { 5, 6 };
		var modified = original.WithBody(newBody);

		modified.Body.Length.ShouldBe(2);
		original.Body.Length.ShouldBe(4);
	}

	[Fact]
	public void WithCorrelationId_ReturnsNewInstance()
	{
		var original = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);
		var modified = original.WithCorrelationId("corr-new");

		modified.CorrelationId.ShouldBe("corr-new");
		original.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void WithMessageType_ReturnsNewInstance()
	{
		var original = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);
		var modified = original.WithMessageType("OrderCreated");

		modified.MessageType.ShouldBe("OrderCreated");
		original.MessageType.ShouldBeNull();
	}

	[Fact]
	public void WithPriority_ReturnsNewInstance()
	{
		var original = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);
		var modified = original.WithPriority(10);

		modified.Priority.ShouldBe((byte)10);
		original.Priority.ShouldBe((byte)0);
	}

	[Fact]
	public void WithTimeToLive_ReturnsNewInstance()
	{
		var original = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);
		var modified = original.WithTimeToLive(TimeSpan.FromMinutes(5));

		modified.TimeToLiveSeconds.ShouldBe(300);
		original.TimeToLiveSeconds.ShouldBe(0);
	}

	[Fact]
	public void SupportEqualityComparison()
	{
		var e1 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, correlationId: "c1");
		var e2 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, correlationId: "c1");
		var e3 = new MessageEnvelopeStruct("msg-2", TestBody, TestTicks, correlationId: "c1");

		(e1 == e2).ShouldBeTrue();
		(e1 != e3).ShouldBeTrue();
		e1.Equals(e2).ShouldBeTrue();
		e1.Equals(e3).ShouldBeFalse();
	}

	[Fact]
	public void SupportEqualityWithHeaders()
	{
		var h1 = new Dictionary<string, string> { ["k"] = "v" };
		var h2 = new Dictionary<string, string> { ["k"] = "v" };
		var h3 = new Dictionary<string, string> { ["k"] = "different" };

		var e1 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, h1);
		var e2 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, h2);
		var e3 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, h3);

		e1.Equals(e2).ShouldBeTrue();
		e1.Equals(e3).ShouldBeFalse();
	}

	[Fact]
	public void SupportEqualityWithNullHeaders()
	{
		var e1 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, headers: null);
		var e2 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, headers: null);
		var e3 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, new Dictionary<string, string>());

		e1.Equals(e2).ShouldBeTrue();
		e1.Equals(e3).ShouldBeFalse();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var e = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);

		e.Equals((object)e).ShouldBeTrue();
		e.Equals(null).ShouldBeFalse();
		e.Equals("not an envelope").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var e1 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);
		var e2 = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);

		e1.GetHashCode().ShouldBe(e2.GetHashCode());
	}

	[Fact]
	public void SupportToString()
	{
		var e = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks, messageType: "OrderCreated");

		var str = e.ToString();

		str.ShouldContain("msg-1");
		str.ShouldContain("OrderCreated");
		str.ShouldContain("4");
	}

	[Fact]
	public void ToStringShowsUnknownWhenNoType()
	{
		var e = new MessageEnvelopeStruct("msg-1", TestBody, TestTicks);

		e.ToString().ShouldContain("unknown");
	}
}
