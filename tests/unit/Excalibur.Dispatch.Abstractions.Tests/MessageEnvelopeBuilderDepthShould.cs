// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Depth coverage tests for <see cref="MessageEnvelopeBuilder{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeBuilderDepthShould
{
	[Fact]
	public void Build_ThrowsInvalidOperationException_WhenMessageIsNull()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithContext(new MessageEnvelope());

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void Build_ThrowsInvalidOperationException_WhenContextIsNull()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(new TestMessage());

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void Build_Succeeds_WithMessageAndContext()
	{
		// Arrange
		var message = new TestMessage();
		var context = new MessageEnvelope { CorrelationId = "corr-1", CausationId = "cause-1" };
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(message)
			.WithContext(context);

		// Act
		var envelope = builder.Build();

		// Assert
		envelope.ShouldNotBeNull();
		envelope.Message.ShouldBeSameAs(message);
		envelope.CorrelationId.ShouldBe("corr-1");
		envelope.CausationId.ShouldBe("cause-1");
		envelope.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Build_UsesCustomMessageId_WhenProvided()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(new TestMessage())
			.WithContext(new MessageEnvelope())
			.WithMessageId("custom-id");

		// Act
		var envelope = builder.Build();

		// Assert
		envelope.MessageId.ShouldBe("custom-id");
	}

	[Fact]
	public void Build_GeneratesMessageId_WhenNotProvided()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(new TestMessage())
			.WithContext(new MessageEnvelope());

		// Act
		var envelope = builder.Build();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Build_GeneratesCorrelationId_WhenContextHasNone()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>()
			.WithMessage(new TestMessage())
			.WithContext(new MessageEnvelope { CorrelationId = null });

		// Act
		var envelope = builder.Build();

		// Assert
		envelope.CorrelationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void FluentApi_ReturnsSameBuilder()
	{
		// Arrange
		var builder = new MessageEnvelopeBuilder<TestMessage>();

		// Act & Assert — all fluent methods return the same builder
		builder.WithMessage(new TestMessage()).ShouldBeSameAs(builder);
		builder.WithContext(new MessageEnvelope()).ShouldBeSameAs(builder);
		builder.WithMessageId("id").ShouldBeSameAs(builder);
		builder.WithTimestamp(DateTimeOffset.UtcNow).ShouldBeSameAs(builder);
		builder.WithRawBody(ReadOnlyMemory<byte>.Empty).ShouldBeSameAs(builder);
	}

	private sealed class TestMessage : IDispatchMessage;
}
