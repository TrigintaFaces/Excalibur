namespace Excalibur.Dispatch.Tests;

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
		using var context = new MessageEnvelope();
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

	/// <summary>
	/// ojazhv engage-test (author≠impl): the builder's optional setters MUST be applied in
	/// <see cref="MessageEnvelopeBuilder{T}.Build"/>, not silently discarded.
	/// </summary>
	/// <remarks>
	/// Structural RED argument: pre-fix, <c>WithTimestamp</c>/<c>WithCallbacks</c> were
	/// <c>_ = param; // Placeholder</c> and <c>Build()</c> wired none — so <c>Timestamp</c>,
	/// <c>AcknowledgeAsync</c> and <c>RejectAsync</c> were dropped. The existing fluent-return test only
	/// asserts <c>ShouldBeSameAs(builder)</c>, which passes against the discarding placeholder; this test
	/// asserts the values reach the built envelope. Removing any of the three Build() assignments
	/// (e.g. <c>AcknowledgeAsync = _onAcknowledge</c>) turns the corresponding assertion RED.
	/// </remarks>
	[Fact]
	public void Build_AppliesTimestampAndCallbacks_FromBuilderSetters()
	{
		// Arrange
		var message = new TestMessage();
		using var context = new MessageEnvelope { CorrelationId = "corr-ojazhv" };
		var timestamp = new DateTimeOffset(2026, 6, 29, 1, 2, 3, TimeSpan.Zero);
		Func<CancellationToken, Task> onAcknowledge = _ => Task.CompletedTask;
		Func<string?, CancellationToken, Task> onReject = (_, _) => Task.CompletedTask;

		// Act
		using var envelope = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(message)
			.WithContext(context)
			.WithTimestamp(timestamp)
			.WithCallbacks(onAcknowledge, onReject)
			.Build();

		// Assert — each setter's value is present on the built envelope (RED if Build() discards it).
		envelope.Timestamp.ShouldBe(timestamp);
		envelope.AcknowledgeAsync.ShouldBeSameAs(onAcknowledge);
		envelope.RejectAsync.ShouldBeSameAs(onReject);
	}

	private sealed class TestMessage : IDispatchMessage;
}