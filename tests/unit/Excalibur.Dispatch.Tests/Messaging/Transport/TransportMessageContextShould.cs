using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportMessageContextShould
{
	[Fact]
	public void CreateWithMessageId()
	{
		var ctx = new TransportMessageContext("msg-1");

		ctx.MessageId.ShouldBe("msg-1");
		ctx.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void CreateWithGeneratedMessageId()
	{
		var ctx = new TransportMessageContext();

		ctx.MessageId.ShouldNotBeNullOrWhiteSpace();
		ctx.MessageId.Length.ShouldBe(32); // Guid "N" format
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		Should.Throw<ArgumentException>(() => new TransportMessageContext(null!));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		Should.Throw<ArgumentException>(() => new TransportMessageContext(""));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		Should.Throw<ArgumentException>(() => new TransportMessageContext("  "));
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var ctx = new TransportMessageContext("msg-1")
		{
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			SourceTransport = "kafka",
			TargetTransport = "rabbitmq",
			ContentType = "application/json",
		};

		ctx.CorrelationId.ShouldBe("corr-1");
		ctx.CausationId.ShouldBe("cause-1");
		ctx.SourceTransport.ShouldBe("kafka");
		ctx.TargetTransport.ShouldBe("rabbitmq");
		ctx.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void SetAndGetTransportProperty()
	{
		var ctx = new TransportMessageContext("msg-1");

		ctx.SetTransportProperty("key", "value");

		ctx.GetTransportProperty<string>("key").ShouldBe("value");
	}

	[Fact]
	public void ReturnDefaultForMissingTransportProperty()
	{
		var ctx = new TransportMessageContext("msg-1");

		ctx.GetTransportProperty<string>("missing").ShouldBeNull();
		ctx.GetTransportProperty<int>("missing").ShouldBe(0);
	}

	[Fact]
	public void ReturnDefaultForWrongTypeTransportProperty()
	{
		var ctx = new TransportMessageContext("msg-1");
		ctx.SetTransportProperty("key", 42);

		ctx.GetTransportProperty<string>("key").ShouldBeNull();
	}

	[Fact]
	public void HasTransportProperty_ReturnsTrue()
	{
		var ctx = new TransportMessageContext("msg-1");
		ctx.SetTransportProperty("key", "value");

		ctx.HasTransportProperty("key").ShouldBeTrue();
	}

	[Fact]
	public void HasTransportProperty_ReturnsFalse()
	{
		var ctx = new TransportMessageContext("msg-1");

		ctx.HasTransportProperty("missing").ShouldBeFalse();
	}

	[Fact]
	public void GetAllTransportProperties()
	{
		var ctx = new TransportMessageContext("msg-1");
		ctx.SetTransportProperty("k1", "v1");
		ctx.SetTransportProperty("k2", 42);

		var all = ctx.GetAllTransportProperties();

		all.Count.ShouldBe(2);
		all["k1"].ShouldBe("v1");
		all["k2"].ShouldBe(42);
	}

	[Fact]
	public void SetHeader()
	{
		var ctx = new TransportMessageContext("msg-1");

		ctx.SetHeader("X-Custom", "value");

		ctx.Headers["X-Custom"].ShouldBe("value");
	}

	[Fact]
	public void SetHeaders_Multiple()
	{
		var ctx = new TransportMessageContext("msg-1");
		var headers = new Dictionary<string, string>
		{
			["X-A"] = "a",
			["X-B"] = "b",
		};

		ctx.SetHeaders(headers);

		ctx.Headers["X-A"].ShouldBe("a");
		ctx.Headers["X-B"].ShouldBe("b");
	}

	[Fact]
	public void SetHeaders_ThrowOnNull()
	{
		var ctx = new TransportMessageContext("msg-1");

		Should.Throw<ArgumentNullException>(() => ctx.SetHeaders(null!));
	}

	[Fact]
	public void RemoveHeader()
	{
		var ctx = new TransportMessageContext("msg-1");
		ctx.SetHeader("X-Remove", "value");

		ctx.RemoveHeader("X-Remove").ShouldBeTrue();

		ctx.Headers.ContainsKey("X-Remove").ShouldBeFalse();
	}

	[Fact]
	public void RemoveHeader_ReturnsFalseWhenNotFound()
	{
		var ctx = new TransportMessageContext("msg-1");

		ctx.RemoveHeader("X-Missing").ShouldBeFalse();
	}

	[Fact]
	public void Headers_AreCaseInsensitive()
	{
		var ctx = new TransportMessageContext("msg-1");
		ctx.SetHeader("X-Custom", "value");

		ctx.Headers["x-custom"].ShouldBe("value");
	}

	[Fact]
	public void TransportProperties_AreCaseInsensitive()
	{
		var ctx = new TransportMessageContext("msg-1");
		ctx.SetTransportProperty("MyKey", "value");

		ctx.HasTransportProperty("mykey").ShouldBeTrue();
		ctx.GetTransportProperty<string>("MYKEY").ShouldBe("value");
	}

	[Fact]
	public void TransportPropertyOperations_ThrowOnNullOrWhitespaceName()
	{
		var ctx = new TransportMessageContext("msg-1");

		Should.Throw<ArgumentException>(() => ctx.GetTransportProperty<string>(null!));
		Should.Throw<ArgumentException>(() => ctx.GetTransportProperty<string>(""));
		Should.Throw<ArgumentException>(() => ctx.SetTransportProperty<string>(null!, "v"));
		Should.Throw<ArgumentException>(() => ctx.HasTransportProperty(null!));
	}

	[Fact]
	public void SetHeader_ThrowOnNullOrWhitespaceName()
	{
		var ctx = new TransportMessageContext("msg-1");

		Should.Throw<ArgumentException>(() => ctx.SetHeader(null!, "value"));
		Should.Throw<ArgumentException>(() => ctx.SetHeader("", "value"));
	}

	[Fact]
	public void RemoveHeader_ThrowOnNullOrWhitespaceName()
	{
		var ctx = new TransportMessageContext("msg-1");

		Should.Throw<ArgumentException>(() => ctx.RemoveHeader(null!));
		Should.Throw<ArgumentException>(() => ctx.RemoveHeader(""));
	}
}
