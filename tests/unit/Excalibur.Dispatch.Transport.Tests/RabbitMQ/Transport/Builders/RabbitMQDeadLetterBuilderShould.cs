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
[Trait(TraitNames.Component, TestComponents.Transport)]
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

	#region MaxRetryAttempts Tests

	#endregion

	#region RetryDelay Tests

	#endregion

	#region Full Fluent Chain Tests

	#endregion
}
