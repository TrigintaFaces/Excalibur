// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IRabbitMQDeadLetterBuilder"/>.
/// Part of S473.4 - Unit tests for RabbitMQ builder (Sprint 473).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMQDeadLetterBuilderShould : UnitTestBase
{
	#region Exchange Tests

	[Fact]
	public void Exchange_ThrowWhenExchangeIsNull()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Exchange(null!));
	}

	[Fact]
	public void Exchange_ThrowWhenExchangeIsEmpty()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Exchange(""));
	}

	[Fact]
	public void Exchange_ThrowWhenExchangeIsWhitespace()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Exchange("   "));
	}

	[Fact]
	public void Exchange_SetExchangeInOptions()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		_ = builder.Exchange("dead-letters");

		// Assert
		options.Exchange.ShouldBe("dead-letters");
	}

	[Fact]
	public void Exchange_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		var result = builder.Exchange("dlx");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Queue Tests

	[Fact]
	public void Queue_ThrowWhenQueueIsNull()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Queue(null!));
	}

	[Fact]
	public void Queue_ThrowWhenQueueIsEmpty()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Queue(""));
	}

	[Fact]
	public void Queue_ThrowWhenQueueIsWhitespace()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Queue("   "));
	}

	[Fact]
	public void Queue_SetQueueInOptions()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		_ = builder.Queue("dead-letter-queue");

		// Assert
		options.Queue.ShouldBe("dead-letter-queue");
	}

	[Fact]
	public void Queue_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		var result = builder.Queue("dlq");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region RoutingKey Tests

	[Fact]
	public void RoutingKey_ThrowWhenRoutingKeyIsNull()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.RoutingKey(null!));
	}

	[Fact]
	public void RoutingKey_AllowEmptyString()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act - Should not throw (empty routing key is valid)
		_ = builder.RoutingKey("");

		// Assert
		options.RoutingKey.ShouldBe("");
	}

	[Fact]
	public void RoutingKey_SetRoutingKeyInOptions()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		_ = builder.RoutingKey("#");

		// Assert
		options.RoutingKey.ShouldBe("#");
	}

	[Fact]
	public void RoutingKey_SupportWildcardPattern()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		_ = builder.RoutingKey("dead.#");

		// Assert
		options.RoutingKey.ShouldBe("dead.#");
	}

	[Fact]
	public void RoutingKey_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		var result = builder.RoutingKey("key");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxRetries Tests

	[Fact]
	public void MaxRetries_ThrowWhenNegative()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxRetries(-1));
	}

	[Fact]
	public void MaxRetries_AllowZero()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act - Should not throw (0 retries means immediate DLQ)
		_ = builder.MaxRetries(0);

		// Assert
		options.MaxRetries.ShouldBe(0);
	}

	[Fact]
	public void MaxRetries_SetMaxRetriesInOptions()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		_ = builder.MaxRetries(3);

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void MaxRetries_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		var result = builder.MaxRetries(5);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region RetryDelay Tests

	[Fact]
	public void RetryDelay_ThrowWhenNegative()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.RetryDelay(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void RetryDelay_AllowZero()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act - Should not throw (immediate retry)
		_ = builder.RetryDelay(TimeSpan.Zero);

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void RetryDelay_SetDelayInOptions()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		_ = builder.RetryDelay(TimeSpan.FromSeconds(30));

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void RetryDelay_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act
		var result = builder.RetryDelay(TimeSpan.FromMinutes(1));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void DeadLetterBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new RabbitMQDeadLetterOptions();
		var builder = new RabbitMQDeadLetterBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.Exchange("dead-letters")
				   .Queue("dead-letter-queue")
				   .RoutingKey("#")
				   .MaxRetries(3)
				   .RetryDelay(TimeSpan.FromSeconds(30));
		});

		// Verify all options set
		options.Exchange.ShouldBe("dead-letters");
		options.Queue.ShouldBe("dead-letter-queue");
		options.RoutingKey.ShouldBe("#");
		options.MaxRetries.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion
}
