namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for MessageEnvelopeBuilder.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeBuilderShould : UnitTestBase
{
	[Fact]
	public void Build_WithAllRequiredFields_ReturnsEnvelope()
	{
		// Arrange
		var message = new TestMessage();
		using var context = new MessageEnvelope { CorrelationId = "corr-123", CausationId = "cause-456" };
		var builder = new MessageEnvelopeBuilder<TestMessage>();

		// Act
		using var envelope = builder
			.WithMessage(message)
			.WithContext(context)
			.WithMessageId("msg-789")
			.Build();

		// Assert
		envelope.ShouldNotBeNull();
		envelope.MessageId.ShouldBe("msg-789");
		envelope.CorrelationId.ShouldBe("corr-123");
		envelope.CausationId.ShouldBe("cause-456");
		envelope.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Build_WithoutMessageId_GeneratesId()
	{
		// Arrange
		var message = new TestMessage();
		using var context = new MessageEnvelope { CorrelationId = "corr-123" };
		var builder = new MessageEnvelopeBuilder<TestMessage>();

		// Act
		using var envelope = builder
			.WithMessage(message)
			.WithContext(context)
			.Build();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Build_WithoutMessage_ThrowsInvalidOperationException()
	{
		// Arrange
		using var context = new MessageEnvelope();
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithContext(context);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void Build_WithoutContext_ThrowsInvalidOperationException()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(new TestMessage());

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void WithTimestamp_ReturnsSameBuilder()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>();

		// Act
		var result = builder.WithTimestamp(DateTimeOffset.UtcNow);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithRawBody_ReturnsSameBuilder()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>();

		// Act
		var result = builder.WithRawBody(new byte[] { 1, 2, 3 });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithCallbacks_ReturnsSameBuilder()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>();

		// Act
		var result = builder.WithCallbacks(
			_ => Task.CompletedTask,
			(_, _) => Task.CompletedTask);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void Build_WithContextMissingCorrelationId_GeneratesCorrelationId()
	{
		// Arrange
		var message = new TestMessage();
		using var context = new MessageEnvelope(); // No CorrelationId set
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(message)
			.WithContext(context);

		// Act
		using var envelope = builder.Build();

		// Assert
		envelope.CorrelationId.ShouldNotBeNullOrEmpty();
	}

	private sealed class TestMessage : IDispatchMessage;
}
