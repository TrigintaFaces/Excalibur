// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IRabbitMQExchangeBuilder"/>.
/// Part of S473.4 - Unit tests for RabbitMQ builder (Sprint 473).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMQExchangeBuilderShould : UnitTestBase
{
	#region Name Tests

	[Fact]
	public void Name_ThrowWhenNameIsNull()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Name(null!));
	}

	[Fact]
	public void Name_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Name(""));
	}

	[Fact]
	public void Name_SetNameInOptions()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.Name("events-exchange");

		// Assert
		options.Name.ShouldBe("events-exchange");
	}

	[Fact]
	public void Name_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		var result = builder.Name("exchange");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Type Tests

	[Theory]
	[InlineData(RabbitMQExchangeType.Direct)]
	[InlineData(RabbitMQExchangeType.Topic)]
	[InlineData(RabbitMQExchangeType.Fanout)]
	[InlineData(RabbitMQExchangeType.Headers)]
	public void Type_SetTypeInOptions(RabbitMQExchangeType exchangeType)
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.Type(exchangeType);

		// Assert
		options.Type.ShouldBe(exchangeType);
	}

	[Fact]
	public void Type_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		var result = builder.Type(RabbitMQExchangeType.Topic);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Durable Tests

	[Fact]
	public void Durable_EnableByDefault()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions { Durable = false };
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.Durable();

		// Assert
		options.Durable.ShouldBeTrue();
	}

	[Fact]
	public void Durable_DisableWhenExplicit()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions { Durable = true };
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.Durable(false);

		// Assert
		options.Durable.ShouldBeFalse();
	}

	[Fact]
	public void Durable_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		var result = builder.Durable();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoDelete Tests

	[Fact]
	public void AutoDelete_DisableByDefault()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions { AutoDelete = true };
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.AutoDelete();

		// Assert
		options.AutoDelete.ShouldBeFalse();
	}

	[Fact]
	public void AutoDelete_EnableWhenExplicit()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions { AutoDelete = false };
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.AutoDelete(true);

		// Assert
		options.AutoDelete.ShouldBeTrue();
	}

	[Fact]
	public void AutoDelete_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		var result = builder.AutoDelete();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Arguments Tests

	[Fact]
	public void Arguments_ThrowWhenArgumentsIsNull()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.Arguments(null!));
	}

	[Fact]
	public void Arguments_AddArgumentsToOptions()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);
		var arguments = new Dictionary<string, object>
		{
			["x-delayed-type"] = "direct",
			["x-custom"] = 123
		};

		// Act
		_ = builder.Arguments(arguments);

		// Assert
		options.Arguments.ShouldContainKey("x-delayed-type");
		options.Arguments["x-delayed-type"].ShouldBe("direct");
		options.Arguments.ShouldContainKey("x-custom");
		options.Arguments["x-custom"].ShouldBe(123);
	}

	[Fact]
	public void Arguments_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

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
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithArgument(null!, "value"));
	}

	[Fact]
	public void WithArgument_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithArgument("", "value"));
	}

	[Fact]
	public void WithArgument_AddArgumentToOptions()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.WithArgument("x-custom", "custom-value");

		// Assert
		options.Arguments.ShouldContainKey("x-custom");
		options.Arguments["x-custom"].ShouldBe("custom-value");
	}

	[Fact]
	public void WithArgument_SupportMultipleArguments()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		_ = builder.WithArgument("key1", "value1")
			   .WithArgument("key2", "value2");

		// Assert
		options.Arguments.Count.ShouldBe(2);
	}

	[Fact]
	public void WithArgument_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act
		var result = builder.WithArgument("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void ExchangeBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.Name("events")
				   .Type(RabbitMQExchangeType.Topic)
				   .Durable(true)
				   .AutoDelete(false)
				   .WithArgument("x-delayed-type", "topic");
		});

		// Verify all options set
		options.Name.ShouldBe("events");
		options.Type.ShouldBe(RabbitMQExchangeType.Topic);
		options.Durable.ShouldBeTrue();
		options.AutoDelete.ShouldBeFalse();
		options.Arguments["x-delayed-type"].ShouldBe("topic");
	}

	#endregion
}
