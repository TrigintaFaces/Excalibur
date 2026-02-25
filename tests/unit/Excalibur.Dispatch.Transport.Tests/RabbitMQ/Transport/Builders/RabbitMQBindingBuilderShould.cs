// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IRabbitMQBindingBuilder"/>.
/// Part of S473.4 - Unit tests for RabbitMQ builder (Sprint 473).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMQBindingBuilderShould : UnitTestBase
{
	#region Exchange Tests

	[Fact]
	public void Exchange_ThrowWhenExchangeIsNull()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Exchange(null!));
	}

	[Fact]
	public void Exchange_ThrowWhenExchangeIsEmpty()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Exchange(""));
	}

	[Fact]
	public void Exchange_SetExchangeInOptions()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		_ = builder.Exchange("events-exchange");

		// Assert
		options.Exchange.ShouldBe("events-exchange");
	}

	[Fact]
	public void Exchange_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		var result = builder.Exchange("exchange");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Queue Tests

	[Fact]
	public void Queue_ThrowWhenQueueIsNull()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Queue(null!));
	}

	[Fact]
	public void Queue_ThrowWhenQueueIsEmpty()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Queue(""));
	}

	[Fact]
	public void Queue_SetQueueInOptions()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		_ = builder.Queue("order-handlers");

		// Assert
		options.Queue.ShouldBe("order-handlers");
	}

	[Fact]
	public void Queue_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		var result = builder.Queue("queue");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region RoutingKey Tests

	[Fact]
	public void RoutingKey_ThrowWhenRoutingKeyIsNull()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.RoutingKey(null!));
	}

	[Fact]
	public void RoutingKey_AllowEmptyString()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act - Should not throw (empty routing key is valid for fanout)
		_ = builder.RoutingKey("");

		// Assert
		options.RoutingKey.ShouldBe("");
	}

	[Fact]
	public void RoutingKey_SetRoutingKeyInOptions()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		_ = builder.RoutingKey("orders.*");

		// Assert
		options.RoutingKey.ShouldBe("orders.*");
	}

	[Fact]
	public void RoutingKey_SupportWildcardPatterns()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		_ = builder.RoutingKey("orders.#");

		// Assert
		options.RoutingKey.ShouldBe("orders.#");
	}

	[Fact]
	public void RoutingKey_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		var result = builder.RoutingKey("key");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Arguments Tests

	[Fact]
	public void Arguments_ThrowWhenArgumentsIsNull()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.Arguments(null!));
	}

	[Fact]
	public void Arguments_AddArgumentsToOptions()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);
		var arguments = new Dictionary<string, object>
		{
			["x-match"] = "all",
			["header-key"] = "header-value"
		};

		// Act
		_ = builder.Arguments(arguments);

		// Assert
		options.Arguments.ShouldContainKey("x-match");
		options.Arguments["x-match"].ShouldBe("all");
	}

	[Fact]
	public void Arguments_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		var result = builder.Arguments(new Dictionary<string, object>());

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithArgument Tests

	[Fact]
	public void WithArgument_ThrowWhenKeyIsNull()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithArgument(null!, "value"));
	}

	[Fact]
	public void WithArgument_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithArgument("", "value"));
	}

	[Fact]
	public void WithArgument_AddArgumentToOptions()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		_ = builder.WithArgument("x-match", "any");

		// Assert
		options.Arguments.ShouldContainKey("x-match");
		options.Arguments["x-match"].ShouldBe("any");
	}

	[Fact]
	public void WithArgument_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act
		var result = builder.WithArgument("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void BindingBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.Exchange("events")
				   .Queue("order-handlers")
				   .RoutingKey("orders.*")
				   .WithArgument("x-match", "all");
		});

		// Verify all options set
		options.Exchange.ShouldBe("events");
		options.Queue.ShouldBe("order-handlers");
		options.RoutingKey.ShouldBe("orders.*");
		options.Arguments["x-match"].ShouldBe("all");
	}

	#endregion
}
