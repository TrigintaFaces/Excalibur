// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.Azure;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAzureServiceBusProcessorBuilder"/>.
/// Part of S472.3 - AddAzureServiceBusTransport single entry point (Sprint 472).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AzureServiceBusProcessorBuilderShould : UnitTestBase
{
	#region DefaultEntity Tests

	[Fact]
	public void DefaultEntity_ThrowWhenEntityNameIsNull()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.DefaultEntity(null!));
	}

	[Fact]
	public void DefaultEntity_ThrowWhenEntityNameIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.DefaultEntity(""));
	}

	[Fact]
	public void DefaultEntity_SetEntityNameInOptions()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.DefaultEntity("orders-queue");

		// Assert
		options.DefaultEntityName.ShouldBe("orders-queue");
	}

	[Fact]
	public void DefaultEntity_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.DefaultEntity("orders-queue");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxConcurrentCalls Tests

	[Fact]
	public void MaxConcurrentCalls_ThrowWhenCountIsZero()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxConcurrentCalls(0));
	}

	[Fact]
	public void MaxConcurrentCalls_ThrowWhenCountIsNegative()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxConcurrentCalls(-1));
	}

	[Fact]
	public void MaxConcurrentCalls_SetCountInOptions()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.MaxConcurrentCalls(20);

		// Assert
		options.MaxConcurrentCalls.ShouldBe(20);
	}

	[Fact]
	public void MaxConcurrentCalls_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.MaxConcurrentCalls(10);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoCompleteMessages Tests

	[Fact]
	public void AutoCompleteMessages_EnableByDefault()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		options.AutoCompleteMessages = false;
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.AutoCompleteMessages();

		// Assert
		options.AutoCompleteMessages.ShouldBeTrue();
	}

	[Fact]
	public void AutoCompleteMessages_DisableWhenExplicit()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		options.AutoCompleteMessages = true;
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.AutoCompleteMessages(false);

		// Assert
		options.AutoCompleteMessages.ShouldBeFalse();
	}

	[Fact]
	public void AutoCompleteMessages_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.AutoCompleteMessages();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region PrefetchCount Tests

	[Fact]
	public void PrefetchCount_ThrowWhenCountIsNegative()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.PrefetchCount(-1));
	}

	[Fact]
	public void PrefetchCount_AllowZero()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act - Should not throw
		_ = builder.PrefetchCount(0);

		// Assert
		options.PrefetchCount.ShouldBe(0);
	}

	[Fact]
	public void PrefetchCount_SetCountInOptions()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.PrefetchCount(100);

		// Assert
		options.PrefetchCount.ShouldBe(100);
	}

	[Fact]
	public void PrefetchCount_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.PrefetchCount(50);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxAutoLockRenewalDuration Tests

	[Fact]
	public void MaxAutoLockRenewalDuration_ThrowWhenDurationIsNegative()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			builder.MaxAutoLockRenewalDuration(TimeSpan.FromMinutes(-1)));
	}

	[Fact]
	public void MaxAutoLockRenewalDuration_AllowZero()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act - Should not throw
		_ = builder.MaxAutoLockRenewalDuration(TimeSpan.Zero);

		// Assert
		options.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void MaxAutoLockRenewalDuration_SetDurationInOptions()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.MaxAutoLockRenewalDuration(TimeSpan.FromMinutes(5));

		// Assert
		options.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void MaxAutoLockRenewalDuration_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.MaxAutoLockRenewalDuration(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ReceiveMode Tests

	[Fact]
	public void ReceiveMode_SetModeInOptions()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.ReceiveMode(ServiceBusReceiveMode.ReceiveAndDelete);

		// Assert
		options.ReceiveMode.ShouldBe(ServiceBusReceiveMode.ReceiveAndDelete);
	}

	[Fact]
	public void ReceiveMode_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.ReceiveMode(ServiceBusReceiveMode.PeekLock);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithConfig Tests

	[Fact]
	public void WithConfig_ThrowWhenKeyIsNull()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig(null!, "value"));
	}

	[Fact]
	public void WithConfig_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig("", "value"));
	}

	[Fact]
	public void WithConfig_AddConfigToOptions()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		_ = builder.WithConfig("custom.key", "custom.value");

		// Assert
		options.AdditionalConfig.ShouldContainKey("custom.key");
		options.AdditionalConfig["custom.key"].ShouldBe("custom.value");
	}

	[Fact]
	public void WithConfig_SupportMultipleConfigs()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

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
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act
		var result = builder.WithConfig("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void ProcessorBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new AzureServiceBusProcessorOptions();
		var builder = new AzureServiceBusProcessorBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.DefaultEntity("orders-queue")
				   .MaxConcurrentCalls(20)
				   .AutoCompleteMessages(false)
				   .PrefetchCount(100)
				   .MaxAutoLockRenewalDuration(TimeSpan.FromMinutes(5))
				   .ReceiveMode(ServiceBusReceiveMode.ReceiveAndDelete)
				   .WithConfig("custom.key", "custom.value");
		});

		// Verify all options set
		options.DefaultEntityName.ShouldBe("orders-queue");
		options.MaxConcurrentCalls.ShouldBe(20);
		options.AutoCompleteMessages.ShouldBeFalse();
		options.PrefetchCount.ShouldBe(100);
		options.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.FromMinutes(5));
		options.ReceiveMode.ShouldBe(ServiceBusReceiveMode.ReceiveAndDelete);
		options.AdditionalConfig["custom.key"].ShouldBe("custom.value");
	}

	#endregion
}
