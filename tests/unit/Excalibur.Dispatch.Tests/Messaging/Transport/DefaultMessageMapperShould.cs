using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultMessageMapperShould
{
	[Fact]
	public void CreateWithName()
	{
		var mapper = new DefaultMessageMapper("test-mapper");

		mapper.Name.ShouldBe("test-mapper");
		mapper.SourceTransport.ShouldBe("*");
		mapper.TargetTransport.ShouldBe("*");
	}

	[Fact]
	public void CreateWithAllParameters()
	{
		var mapper = new DefaultMessageMapper("mapper", "kafka", "rabbitmq");

		mapper.Name.ShouldBe("mapper");
		mapper.SourceTransport.ShouldBe("kafka");
		mapper.TargetTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void ThrowOnNullName()
	{
		Should.Throw<ArgumentException>(() => new DefaultMessageMapper(null!));
	}

	[Fact]
	public void ThrowOnEmptyName()
	{
		Should.Throw<ArgumentException>(() => new DefaultMessageMapper(""));
	}

	[Fact]
	public void HaveWildcardConstant()
	{
		DefaultMessageMapper.WildcardTransport.ShouldBe("*");
	}

	[Fact]
	public void CanMap_WildcardMatchesAny()
	{
		var mapper = new DefaultMessageMapper("test");

		mapper.CanMap("kafka", "rabbitmq").ShouldBeTrue();
		mapper.CanMap("any", "other").ShouldBeTrue();
	}

	[Fact]
	public void CanMap_SpecificSourceAndTarget()
	{
		var mapper = new DefaultMessageMapper("test", "kafka", "rabbitmq");

		mapper.CanMap("kafka", "rabbitmq").ShouldBeTrue();
		mapper.CanMap("kafka", "sqs").ShouldBeFalse();
		mapper.CanMap("sqs", "rabbitmq").ShouldBeFalse();
	}

	[Fact]
	public void CanMap_CaseInsensitive()
	{
		var mapper = new DefaultMessageMapper("test", "kafka", "rabbitmq");

		mapper.CanMap("Kafka", "RabbitMQ").ShouldBeTrue();
		mapper.CanMap("KAFKA", "RABBITMQ").ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ThrowOnNullSource()
	{
		var mapper = new DefaultMessageMapper("test");

		Should.Throw<ArgumentException>(() => mapper.CanMap(null!, "target"));
	}

	[Fact]
	public void CanMap_ThrowOnNullTarget()
	{
		var mapper = new DefaultMessageMapper("test");

		Should.Throw<ArgumentException>(() => mapper.CanMap("source", null!));
	}

	[Fact]
	public void Map_CopiesCommonProperties()
	{
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-1")
		{
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			SourceTransport = "kafka",
			ContentType = "application/json",
		};
		source.SetHeader("X-Key", "value");
		source.SetTransportProperty("prop1", "val1");

		var result = mapper.Map(source, "rabbitmq");

		result.MessageId.ShouldBe("msg-1");
		result.CorrelationId.ShouldBe("corr-1");
		result.CausationId.ShouldBe("cause-1");
		result.SourceTransport.ShouldBe("kafka");
		result.TargetTransport.ShouldBe("rabbitmq");
		result.ContentType.ShouldBe("application/json");
		result.Headers["X-Key"].ShouldBe("value");
	}

	[Fact]
	public void Map_ThrowOnNullSource()
	{
		var mapper = new DefaultMessageMapper("test");

		Should.Throw<ArgumentNullException>(() => mapper.Map(null!, "target"));
	}

	[Fact]
	public void Map_ThrowOnNullTarget()
	{
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-1");

		Should.Throw<ArgumentException>(() => mapper.Map(source, null!));
	}
}
