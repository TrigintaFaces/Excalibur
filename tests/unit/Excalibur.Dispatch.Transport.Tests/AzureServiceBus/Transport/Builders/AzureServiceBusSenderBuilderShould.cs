// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAzureServiceBusSenderBuilder"/>.
/// Part of S472.3 - AddAzureServiceBusTransport single entry point (Sprint 472).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AzureServiceBusSenderBuilderShould : UnitTestBase
{
	#region DefaultEntity Tests

	[Fact]
	public void DefaultEntity_ThrowWhenEntityNameIsNull()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.DefaultEntity(null!));
	}

	[Fact]
	public void DefaultEntity_ThrowWhenEntityNameIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.DefaultEntity(""));
	}

	[Fact]
	public void DefaultEntity_SetEntityNameInOptions()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		_ = builder.DefaultEntity("orders-queue");

		// Assert
		options.DefaultEntityName.ShouldBe("orders-queue");
	}

	[Fact]
	public void DefaultEntity_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		var result = builder.DefaultEntity("orders-queue");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region EnableBatching Tests

	[Fact]
	public void EnableBatching_EnableByDefault()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		options.EnableBatching = false;
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		_ = builder.EnableBatching();

		// Assert
		options.EnableBatching.ShouldBeTrue();
	}

	[Fact]
	public void EnableBatching_DisableWhenExplicit()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		options.EnableBatching = true;
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		_ = builder.EnableBatching(false);

		// Assert
		options.EnableBatching.ShouldBeFalse();
	}

	[Fact]
	public void EnableBatching_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		var result = builder.EnableBatching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxBatchSizeBytes Tests

	[Fact]
	public void MaxBatchSizeBytes_ThrowWhenSizeIsZero()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchSizeBytes(0));
	}

	[Fact]
	public void MaxBatchSizeBytes_ThrowWhenSizeIsNegative()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchSizeBytes(-1));
	}

	[Fact]
	public void MaxBatchSizeBytes_SetSizeInOptions()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		_ = builder.MaxBatchSizeBytes(512 * 1024);

		// Assert
		options.MaxBatchSizeBytes.ShouldBe(512 * 1024);
	}

	[Fact]
	public void MaxBatchSizeBytes_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		var result = builder.MaxBatchSizeBytes(256 * 1024);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MaxBatchCount Tests

	[Fact]
	public void MaxBatchCount_ThrowWhenCountIsZero()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchCount(0));
	}

	[Fact]
	public void MaxBatchCount_ThrowWhenCountIsNegative()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxBatchCount(-1));
	}

	[Fact]
	public void MaxBatchCount_SetCountInOptions()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		_ = builder.MaxBatchCount(50);

		// Assert
		options.MaxBatchCount.ShouldBe(50);
	}

	[Fact]
	public void MaxBatchCount_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		var result = builder.MaxBatchCount(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region BatchWindow Tests

	[Fact]
	public void BatchWindow_ThrowWhenWindowIsNegative()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.BatchWindow(TimeSpan.FromMilliseconds(-1)));
	}

	[Fact]
	public void BatchWindow_AllowZero()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act - Should not throw
		_ = builder.BatchWindow(TimeSpan.Zero);

		// Assert
		options.BatchWindow.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void BatchWindow_SetWindowInOptions()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		_ = builder.BatchWindow(TimeSpan.FromMilliseconds(200));

		// Assert
		options.BatchWindow.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

	[Fact]
	public void BatchWindow_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		var result = builder.BatchWindow(TimeSpan.FromMilliseconds(100));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithConfig Tests

	[Fact]
	public void WithConfig_ThrowWhenKeyIsNull()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig(null!, "value"));
	}

	[Fact]
	public void WithConfig_ThrowWhenKeyIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithConfig("", "value"));
	}

	[Fact]
	public void WithConfig_AddConfigToOptions()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

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
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

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
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act
		var result = builder.WithConfig("key", "value");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void SenderBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new AzureServiceBusSenderOptions();
		var builder = new AzureServiceBusSenderBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.DefaultEntity("orders-queue")
				   .EnableBatching(true)
				   .MaxBatchSizeBytes(512 * 1024)
				   .MaxBatchCount(50)
				   .BatchWindow(TimeSpan.FromMilliseconds(200))
				   .WithConfig("custom.key", "custom.value");
		});

		// Verify all options set
		options.DefaultEntityName.ShouldBe("orders-queue");
		options.EnableBatching.ShouldBeTrue();
		options.MaxBatchSizeBytes.ShouldBe(512 * 1024);
		options.MaxBatchCount.ShouldBe(50);
		options.BatchWindow.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.AdditionalConfig["custom.key"].ShouldBe("custom.value");
	}

	#endregion
}
