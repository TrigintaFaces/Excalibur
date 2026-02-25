// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="DefaultMessageMapper"/>.
/// </summary>
/// <remarks>
/// Tests the default message mapper implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class DefaultMessageMapperShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsName()
	{
		// Arrange & Act
		var mapper = new DefaultMessageMapper("test-mapper");

		// Assert
		mapper.Name.ShouldBe("test-mapper");
	}

	[Fact]
	public void Constructor_DefaultsSourceTransportToWildcard()
	{
		// Arrange & Act
		var mapper = new DefaultMessageMapper("test-mapper");

		// Assert
		mapper.SourceTransport.ShouldBe(DefaultMessageMapper.WildcardTransport);
	}

	[Fact]
	public void Constructor_DefaultsTargetTransportToWildcard()
	{
		// Arrange & Act
		var mapper = new DefaultMessageMapper("test-mapper");

		// Assert
		mapper.TargetTransport.ShouldBe(DefaultMessageMapper.WildcardTransport);
	}

	[Fact]
	public void Constructor_WithAllParameters_SetsAllProperties()
	{
		// Arrange & Act
		var mapper = new DefaultMessageMapper("test-mapper", "rabbitmq", "kafka");

		// Assert
		mapper.Name.ShouldBe("test-mapper");
		mapper.SourceTransport.ShouldBe("rabbitmq");
		mapper.TargetTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new DefaultMessageMapper(null!));
	}

	[Fact]
	public void Constructor_WithEmptyName_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new DefaultMessageMapper(string.Empty));
	}

	[Fact]
	public void Constructor_WithWhitespaceName_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new DefaultMessageMapper("   "));
	}

	[Fact]
	public void Constructor_WithNullSourceTransport_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new DefaultMessageMapper("test", null!, "kafka"));
	}

	[Fact]
	public void Constructor_WithNullTargetTransport_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new DefaultMessageMapper("test", "rabbitmq", null!));
	}

	#endregion

	#region WildcardTransport Tests

	[Fact]
	public void WildcardTransport_IsAsterisk()
	{
		// Assert
		DefaultMessageMapper.WildcardTransport.ShouldBe("*");
	}

	#endregion

	#region CanMap Tests

	[Fact]
	public void CanMap_WithWildcardSource_ReturnsTrue()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "*", "kafka");

		// Act
		var result = mapper.CanMap("rabbitmq", "kafka");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_WithWildcardTarget_ReturnsTrue()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "rabbitmq", "*");

		// Act
		var result = mapper.CanMap("rabbitmq", "kafka");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_WithBothWildcards_ReturnsTrue()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "*", "*");

		// Act
		var result = mapper.CanMap("any-source", "any-target");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_WithExactMatch_ReturnsTrue()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "rabbitmq", "kafka");

		// Act
		var result = mapper.CanMap("rabbitmq", "kafka");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_WithCaseInsensitiveMatch_ReturnsTrue()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "RabbitMQ", "Kafka");

		// Act
		var result = mapper.CanMap("rabbitmq", "kafka");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_WithSourceMismatch_ReturnsFalse()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "rabbitmq", "kafka");

		// Act
		var result = mapper.CanMap("sqs", "kafka");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanMap_WithTargetMismatch_ReturnsFalse()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test", "rabbitmq", "kafka");

		// Act
		var result = mapper.CanMap("rabbitmq", "sqs");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanMap_WithNullSourceTransport_ThrowsArgumentException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.CanMap(null!, "kafka"));
	}

	[Fact]
	public void CanMap_WithNullTargetTransport_ThrowsArgumentException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.CanMap("rabbitmq", null!));
	}

	[Fact]
	public void CanMap_WithEmptySourceTransport_ThrowsArgumentException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.CanMap(string.Empty, "kafka"));
	}

	[Fact]
	public void CanMap_WithEmptyTargetTransport_ThrowsArgumentException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.CanMap("rabbitmq", string.Empty));
	}

	#endregion

	#region Map Tests

	[Fact]
	public void Map_CopiesMessageId()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void Map_CopiesCorrelationId()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123") { CorrelationId = "corr-456" };

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.CorrelationId.ShouldBe("corr-456");
	}

	[Fact]
	public void Map_CopiesCausationId()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123") { CausationId = "cause-789" };

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.CausationId.ShouldBe("cause-789");
	}

	[Fact]
	public void Map_CopiesSourceTransport()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123") { SourceTransport = "rabbitmq" };

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.SourceTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void Map_SetsTargetTransport()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.TargetTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Map_CopiesTimestamp()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
		var source = new TransportMessageContext("msg-123") { Timestamp = timestamp };

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void Map_CopiesContentType()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123") { ContentType = "application/json" };

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Map_CopiesHeaders()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");
		source.SetHeader("x-custom", "custom-value");
		source.SetHeader("x-another", "another-value");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.Headers["x-custom"].ShouldBe("custom-value");
		result.Headers["x-another"].ShouldBe("another-value");
	}

	[Fact]
	public void Map_CopiesTransportProperties()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");
		source.SetTransportProperty("custom-prop", "custom-value");
		source.SetTransportProperty("number-prop", 42);

		// Act
		var result = mapper.Map(source, "generic");

		// Assert
		result.GetTransportProperty<string>("custom-prop").ShouldBe("custom-value");
		result.GetTransportProperty<int>("number-prop").ShouldBe(42);
	}

	[Fact]
	public void Map_ToRabbitMq_ReturnsRabbitMqContext()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		_ = result.ShouldBeOfType<RabbitMqMessageContext>();
	}

	[Fact]
	public void Map_ToKafka_ReturnsKafkaContext()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		_ = result.ShouldBeOfType<KafkaMessageContext>();
	}

	[Fact]
	public void Map_ToRabbitMqCaseInsensitive_ReturnsRabbitMqContext()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "RabbitMQ");

		// Assert
		_ = result.ShouldBeOfType<RabbitMqMessageContext>();
	}

	[Fact]
	public void Map_ToKafkaCaseInsensitive_ReturnsKafkaContext()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "KAFKA");

		// Assert
		_ = result.ShouldBeOfType<KafkaMessageContext>();
	}

	[Fact]
	public void Map_ToUnknownTransport_ReturnsGenericContext()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act
		var result = mapper.Map(source, "sqs");

		// Assert
		_ = result.ShouldBeOfType<TransportMessageContext>();
	}

	[Fact]
	public void Map_WithNullSource_ThrowsArgumentNullException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => mapper.Map(null!, "kafka"));
	}

	[Fact]
	public void Map_WithNullTargetTransportName_ThrowsArgumentException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, null!));
	}

	[Fact]
	public void Map_WithEmptyTargetTransportName_ThrowsArgumentException()
	{
		// Arrange
		var mapper = new DefaultMessageMapper("test");
		var source = new TransportMessageContext("msg-123");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, string.Empty));
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMessageMapper()
	{
		// Arrange & Act
		var mapper = new DefaultMessageMapper("test");

		// Assert
		_ = mapper.ShouldBeAssignableTo<IMessageMapper>();
	}

	#endregion
}
