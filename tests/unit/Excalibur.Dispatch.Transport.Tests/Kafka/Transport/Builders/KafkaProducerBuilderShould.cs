// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IKafkaProducerBuilder"/>.
/// Part of S472.2 - AddKafkaTransport single entry point (Sprint 472).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class KafkaProducerBuilderShould : UnitTestBase
{
	#region ClientId Tests

	[Fact]
	public void ClientId_ThrowWhenClientIdIsNull()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.ClientId(null!));
	}

	[Fact]
	public void ClientId_ThrowWhenClientIdIsEmpty()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.ClientId(""));
	}

	[Fact]
	public void ClientId_SetClientIdInOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.ClientId("my-producer");

		// Assert
		options.ClientId.ShouldBe("my-producer");
	}

	[Fact]
	public void ClientId_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.ClientId("my-producer");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Acks Tests

	[Fact]
	public void Acks_SetAcksInOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.Acks(KafkaAckLevel.Leader);

		// Assert
		options.Acks.ShouldBe(KafkaAckLevel.Leader);
	}

	[Fact]
	public void Acks_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.Acks(KafkaAckLevel.All);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region EnableIdempotence Tests

	[Fact]
	public void EnableIdempotence_EnableByDefault()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.EnableIdempotence();

		// Assert
		options.EnableIdempotence.ShouldBeTrue();
	}

	[Fact]
	public void EnableIdempotence_DisableWhenExplicit()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		options.EnableIdempotence = true;
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.EnableIdempotence(false);

		// Assert
		options.EnableIdempotence.ShouldBeFalse();
	}

	[Fact]
	public void EnableIdempotence_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.EnableIdempotence();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region CompressionType Tests

	[Fact]
	public void CompressionType_SetCompressionInOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.CompressionType(KafkaCompressionType.Snappy);

		// Assert
		options.CompressionType.ShouldBe(KafkaCompressionType.Snappy);
	}

	[Fact]
	public void CompressionType_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.CompressionType(KafkaCompressionType.Gzip);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region EnableTransactions Tests

	[Fact]
	public void EnableTransactions_ThrowWhenTransactionalIdIsNull()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.EnableTransactions(null!));
	}

	[Fact]
	public void EnableTransactions_ThrowWhenTransactionalIdIsEmpty()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.EnableTransactions(""));
	}

	[Fact]
	public void EnableTransactions_SetTransactionsInOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.EnableTransactions("my-txn-id");

		// Assert
		options.EnableTransactions.ShouldBeTrue();
		options.TransactionalId.ShouldBe("my-txn-id");
	}

	[Fact]
	public void EnableTransactions_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.EnableTransactions("my-txn-id");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region LingerMs Tests

	[Fact]
	public void LingerMs_ThrowWhenLingerIsNegative()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.LingerMs(TimeSpan.FromMilliseconds(-1)));
	}

	[Fact]
	public void LingerMs_AllowZero()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act - Should not throw
		_ = builder.LingerMs(TimeSpan.Zero);

		// Assert
		options.LingerMs.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void LingerMs_SetLingerInOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.LingerMs(TimeSpan.FromMilliseconds(10));

		// Assert
		options.LingerMs.ShouldBe(TimeSpan.FromMilliseconds(10));
	}

	[Fact]
	public void LingerMs_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.LingerMs(TimeSpan.FromMilliseconds(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region BatchSize Tests

	[Fact]
	public void BatchSize_ThrowWhenBatchSizeIsZero()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.BatchSize(0));
	}

	[Fact]
	public void BatchSize_ThrowWhenBatchSizeIsNegative()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.BatchSize(-1));
	}

	[Fact]
	public void BatchSize_SetBatchSizeInOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.BatchSize(32768);

		// Assert
		options.BatchSize.ShouldBe(32768);
	}

	[Fact]
	public void BatchSize_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.BatchSize(16384);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithConfig Tests

	[Fact]
	public void WithConfig_ThrowWhenKeyIsNull()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig(null!, "value"));
	}

	[Fact]
	public void WithConfig_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig("", "value"));
	}

	[Fact]
	public void WithConfig_AddConfigToOptions()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.WithConfig("batch.num.messages", "100");

		// Assert
		options.AdditionalConfig.ShouldContainKey("batch.num.messages");
		options.AdditionalConfig["batch.num.messages"].ShouldBe("100");
	}

	[Fact]
	public void WithConfig_SupportMultipleConfigs()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		_ = builder.WithConfig("key1", "value1")
			   .WithConfig("key2", "value2");

		// Assert
		options.AdditionalConfig.Count.ShouldBe(2);
	}

	[Fact]
	public void WithConfig_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act
		var result = builder.WithConfig("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void ProducerBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new KafkaProducerOptions();
		var builder = new KafkaProducerBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.ClientId("my-producer")
				   .Acks(KafkaAckLevel.All)
				   .EnableIdempotence(true)
				   .CompressionType(KafkaCompressionType.Snappy)
				   .LingerMs(TimeSpan.FromMilliseconds(10))
				   .BatchSize(32768)
				   .EnableTransactions("my-txn-id")
				   .WithConfig("custom.key", "custom.value");
		});

		// Verify all options set
		options.ClientId.ShouldBe("my-producer");
		options.Acks.ShouldBe(KafkaAckLevel.All);
		options.EnableIdempotence.ShouldBeTrue();
		options.CompressionType.ShouldBe(KafkaCompressionType.Snappy);
		options.LingerMs.ShouldBe(TimeSpan.FromMilliseconds(10));
		options.BatchSize.ShouldBe(32768);
		options.EnableTransactions.ShouldBeTrue();
		options.TransactionalId.ShouldBe("my-txn-id");
		options.AdditionalConfig["custom.key"].ShouldBe("custom.value");
	}

	#endregion
}
