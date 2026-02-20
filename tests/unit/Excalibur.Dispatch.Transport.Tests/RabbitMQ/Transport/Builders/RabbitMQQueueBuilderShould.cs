// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IRabbitMQQueueBuilder"/>.
/// Part of S473.4 - Unit tests for RabbitMQ builder (Sprint 473).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMQQueueBuilderShould : UnitTestBase
{
	#region Name Tests

	[Fact]
	public void Name_ThrowWhenNameIsNull()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Name(null!));
	}

	[Fact]
	public void Name_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Name(""));
	}

	[Fact]
	public void Name_SetNameInOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.Name("order-handlers");

		// Assert
		options.Name.ShouldBe("order-handlers");
	}

	[Fact]
	public void Name_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.Name("queue");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Durable Tests

	[Fact]
	public void Durable_EnableByDefault()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { Durable = false };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.Durable();

		// Assert
		options.Durable.ShouldBeTrue();
	}

	[Fact]
	public void Durable_DisableWhenExplicit()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { Durable = true };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.Durable(false);

		// Assert
		options.Durable.ShouldBeFalse();
	}

	[Fact]
	public void Durable_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.Durable();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Exclusive Tests

	[Fact]
	public void Exclusive_DisableByDefault()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { Exclusive = true };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.Exclusive();

		// Assert
		options.Exclusive.ShouldBeFalse();
	}

	[Fact]
	public void Exclusive_EnableWhenExplicit()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { Exclusive = false };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.Exclusive(true);

		// Assert
		options.Exclusive.ShouldBeTrue();
	}

	[Fact]
	public void Exclusive_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.Exclusive();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoDelete Tests

	[Fact]
	public void AutoDelete_DisableByDefault()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { AutoDelete = true };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.AutoDelete();

		// Assert
		options.AutoDelete.ShouldBeFalse();
	}

	[Fact]
	public void AutoDelete_EnableWhenExplicit()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { AutoDelete = false };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.AutoDelete(true);

		// Assert
		options.AutoDelete.ShouldBeTrue();
	}

	[Fact]
	public void AutoDelete_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.AutoDelete();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region PrefetchCount Tests

	[Fact]
	public void PrefetchCount_SetCountInOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.PrefetchCount(20);

		// Assert
		options.PrefetchCount.ShouldBe((ushort)20);
	}

	[Fact]
	public void PrefetchCount_AllowZero()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act - Should not throw
		_ = builder.PrefetchCount(0);

		// Assert
		options.PrefetchCount.ShouldBe((ushort)0);
	}

	[Fact]
	public void PrefetchCount_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.PrefetchCount(10);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoAck Tests

	[Fact]
	public void AutoAck_DisableByDefault()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { AutoAck = true };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.AutoAck();

		// Assert
		options.AutoAck.ShouldBeFalse();
	}

	[Fact]
	public void AutoAck_EnableWhenExplicit()
	{
		// Arrange
		var options = new RabbitMQQueueOptions { AutoAck = false };
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.AutoAck(true);

		// Assert
		options.AutoAck.ShouldBeTrue();
	}

	[Fact]
	public void AutoAck_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.AutoAck();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MessageTtl Tests

	[Fact]
	public void MessageTtl_ThrowWhenTtlIsNegative()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MessageTtl(TimeSpan.FromMinutes(-1)));
	}

	[Fact]
	public void MessageTtl_AllowZero()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act - Should not throw
		_ = builder.MessageTtl(TimeSpan.Zero);

		// Assert
		options.MessageTtl.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void MessageTtl_SetTtlInOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.MessageTtl(TimeSpan.FromMinutes(30));

		// Assert
		options.MessageTtl.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void MessageTtl_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.MessageTtl(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxLength Tests

	[Fact]
	public void MaxLength_ThrowWhenLengthIsZero()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxLength(0));
	}

	[Fact]
	public void MaxLength_ThrowWhenLengthIsNegative()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxLength(-1));
	}

	[Fact]
	public void MaxLength_SetLengthInOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.MaxLength(10000);

		// Assert
		options.MaxLength.ShouldBe(10000);
	}

	[Fact]
	public void MaxLength_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.MaxLength(1000);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxLengthBytes Tests

	[Fact]
	public void MaxLengthBytes_ThrowWhenBytesIsZero()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxLengthBytes(0));
	}

	[Fact]
	public void MaxLengthBytes_ThrowWhenBytesIsNegative()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxLengthBytes(-1));
	}

	[Fact]
	public void MaxLengthBytes_SetBytesInOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.MaxLengthBytes(1024 * 1024 * 100); // 100MB

		// Assert
		options.MaxLengthBytes.ShouldBe(1024 * 1024 * 100);
	}

	[Fact]
	public void MaxLengthBytes_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.MaxLengthBytes(1024 * 1024);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Arguments Tests

	[Fact]
	public void Arguments_ThrowWhenArgumentsIsNull()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.Arguments(null!));
	}

	[Fact]
	public void Arguments_AddArgumentsToOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);
		var arguments = new Dictionary<string, object>
		{
			["x-queue-mode"] = "lazy",
			["x-max-priority"] = 10
		};

		// Act
		_ = builder.Arguments(arguments);

		// Assert
		options.Arguments.ShouldContainKey("x-queue-mode");
		options.Arguments["x-queue-mode"].ShouldBe("lazy");
		options.Arguments.ShouldContainKey("x-max-priority");
		options.Arguments["x-max-priority"].ShouldBe(10);
	}

	[Fact]
	public void Arguments_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

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
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithArgument(null!, "value"));
	}

	[Fact]
	public void WithArgument_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithArgument("", "value"));
	}

	[Fact]
	public void WithArgument_AddArgumentToOptions()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		_ = builder.WithArgument("x-queue-type", "quorum");

		// Assert
		options.Arguments.ShouldContainKey("x-queue-type");
		options.Arguments["x-queue-type"].ShouldBe("quorum");
	}

	[Fact]
	public void WithArgument_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act
		var result = builder.WithArgument("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void QueueBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.Name("order-handlers")
				   .Durable(true)
				   .Exclusive(false)
				   .AutoDelete(false)
				   .PrefetchCount(20)
				   .AutoAck(false)
				   .MessageTtl(TimeSpan.FromHours(1))
				   .MaxLength(10000)
				   .MaxLengthBytes(1024 * 1024 * 100)
				   .WithArgument("x-queue-type", "quorum");
		});

		// Verify all options set
		options.Name.ShouldBe("order-handlers");
		options.Durable.ShouldBeTrue();
		options.Exclusive.ShouldBeFalse();
		options.AutoDelete.ShouldBeFalse();
		options.PrefetchCount.ShouldBe((ushort)20);
		options.AutoAck.ShouldBeFalse();
		options.MessageTtl.ShouldBe(TimeSpan.FromHours(1));
		options.MaxLength.ShouldBe(10000);
		options.MaxLengthBytes.ShouldBe(1024 * 1024 * 100);
		options.Arguments["x-queue-type"].ShouldBe("quorum");
	}

	#endregion
}
