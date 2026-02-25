// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IKafkaConsumerBuilder"/>.
/// Part of S472.2 - AddKafkaTransport single entry point (Sprint 472).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class KafkaConsumerBuilderShould : UnitTestBase
{
	#region GroupId Tests

	[Fact]
	public void GroupId_ThrowWhenGroupIdIsNull()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.GroupId(null!));
	}

	[Fact]
	public void GroupId_ThrowWhenGroupIdIsEmpty()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.GroupId(""));
	}

	[Fact]
	public void GroupId_SetGroupIdInOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.GroupId("my-consumer-group");

		// Assert
		options.GroupId.ShouldBe("my-consumer-group");
	}

	[Fact]
	public void GroupId_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.GroupId("my-group");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoOffsetReset Tests

	[Fact]
	public void AutoOffsetReset_SetOffsetResetInOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.AutoOffsetReset(KafkaOffsetReset.Earliest);

		// Assert
		options.AutoOffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
	}

	[Fact]
	public void AutoOffsetReset_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.AutoOffsetReset(KafkaOffsetReset.Latest);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region EnableAutoCommit Tests

	[Fact]
	public void EnableAutoCommit_EnableByDefault()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.EnableAutoCommit();

		// Assert
		options.EnableAutoCommit.ShouldBeTrue();
	}

	[Fact]
	public void EnableAutoCommit_DisableWhenExplicit()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		options.EnableAutoCommit = true;
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.EnableAutoCommit(false);

		// Assert
		options.EnableAutoCommit.ShouldBeFalse();
	}

	[Fact]
	public void EnableAutoCommit_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.EnableAutoCommit();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoCommitInterval Tests

	[Fact]
	public void AutoCommitInterval_ThrowWhenIntervalIsZero()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.AutoCommitInterval(TimeSpan.Zero));
	}

	[Fact]
	public void AutoCommitInterval_ThrowWhenIntervalIsNegative()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.AutoCommitInterval(TimeSpan.FromMilliseconds(-1)));
	}

	[Fact]
	public void AutoCommitInterval_SetIntervalInOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.AutoCommitInterval(TimeSpan.FromSeconds(10));

		// Assert
		options.AutoCommitInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AutoCommitInterval_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.AutoCommitInterval(TimeSpan.FromSeconds(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region SessionTimeout Tests

	[Fact]
	public void SessionTimeout_ThrowWhenTimeoutIsZero()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.SessionTimeout(TimeSpan.Zero));
	}

	[Fact]
	public void SessionTimeout_ThrowWhenTimeoutIsNegative()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.SessionTimeout(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void SessionTimeout_SetTimeoutInOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.SessionTimeout(TimeSpan.FromSeconds(45));

		// Assert
		options.SessionTimeout.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void SessionTimeout_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.SessionTimeout(TimeSpan.FromSeconds(30));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxPollInterval Tests

	[Fact]
	public void MaxPollInterval_ThrowWhenIntervalIsZero()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxPollInterval(TimeSpan.Zero));
	}

	[Fact]
	public void MaxPollInterval_ThrowWhenIntervalIsNegative()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxPollInterval(TimeSpan.FromMinutes(-1)));
	}

	[Fact]
	public void MaxPollInterval_SetIntervalInOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.MaxPollInterval(TimeSpan.FromMinutes(10));

		// Assert
		options.MaxPollInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void MaxPollInterval_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.MaxPollInterval(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxBatchSize Tests

	[Fact]
	public void MaxBatchSize_ThrowWhenBatchSizeIsZero()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchSize(0));
	}

	[Fact]
	public void MaxBatchSize_ThrowWhenBatchSizeIsNegative()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchSize(-1));
	}

	[Fact]
	public void MaxBatchSize_SetBatchSizeInOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.MaxBatchSize(500);

		// Assert
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void MaxBatchSize_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.MaxBatchSize(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithConfig Tests

	[Fact]
	public void WithConfig_ThrowWhenKeyIsNull()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig(null!, "value"));
	}

	[Fact]
	public void WithConfig_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig("", "value"));
	}

	[Fact]
	public void WithConfig_AddConfigToOptions()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		_ = builder.WithConfig("fetch.min.bytes", "1024");

		// Assert
		options.AdditionalConfig.ShouldContainKey("fetch.min.bytes");
		options.AdditionalConfig["fetch.min.bytes"].ShouldBe("1024");
	}

	[Fact]
	public void WithConfig_SupportMultipleConfigs()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

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
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act
		var result = builder.WithConfig("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void ConsumerBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new KafkaConsumerOptions();
		var builder = new KafkaConsumerBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.GroupId("my-consumer-group")
				   .AutoOffsetReset(KafkaOffsetReset.Earliest)
				   .EnableAutoCommit(true)
				   .AutoCommitInterval(TimeSpan.FromSeconds(10))
				   .SessionTimeout(TimeSpan.FromSeconds(45))
				   .MaxPollInterval(TimeSpan.FromMinutes(10))
				   .MaxBatchSize(500)
				   .WithConfig("custom.key", "custom.value");
		});

		// Verify all options set
		options.GroupId.ShouldBe("my-consumer-group");
		options.AutoOffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
		options.EnableAutoCommit.ShouldBeTrue();
		options.AutoCommitInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.SessionTimeout.ShouldBe(TimeSpan.FromSeconds(45));
		options.MaxPollInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.MaxBatchSize.ShouldBe(500);
		options.AdditionalConfig["custom.key"].ShouldBe("custom.value");
	}

	#endregion
}
