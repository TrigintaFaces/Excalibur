// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="OutboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class OutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_BatchSize_IsOneHundred()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void Default_PublishIntervalMs_IsOneThousand()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.PublishIntervalMs.ShouldBe(1000);
	}

	[Fact]
	public void Default_MaxRetries_IsThree()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void Default_SentMessageRetention_IsOneDay()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.SentMessageRetention.ShouldBe(TimeSpan.FromDays(1));
	}

	[Fact]
	public void Default_UseInMemoryStorage_IsFalse()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.UseInMemoryStorage.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void BatchSize_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.BatchSize = 500;

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void PublishIntervalMs_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.PublishIntervalMs = 5000;

		// Assert
		options.PublishIntervalMs.ShouldBe(5000);
	}

	[Fact]
	public void MaxRetries_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.MaxRetries = 10;

		// Assert
		options.MaxRetries.ShouldBe(10);
	}

	[Fact]
	public void SentMessageRetention_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.SentMessageRetention = TimeSpan.FromDays(7);

		// Assert
		options.SentMessageRetention.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void UseInMemoryStorage_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.UseInMemoryStorage = true;

		// Assert
		options.UseInMemoryStorage.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new OutboxOptions
		{
			Enabled = false,
			BatchSize = 250,
			PublishIntervalMs = 2000,
			MaxRetries = 5,
			SentMessageRetention = TimeSpan.FromDays(3),
			UseInMemoryStorage = true,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.BatchSize.ShouldBe(250);
		options.PublishIntervalMs.ShouldBe(2000);
		options.MaxRetries.ShouldBe(5);
		options.SentMessageRetention.ShouldBe(TimeSpan.FromDays(3));
		options.UseInMemoryStorage.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeBatchSize()
	{
		// Act
		var options = new OutboxOptions
		{
			BatchSize = 1000,
			PublishIntervalMs = 500,
		};

		// Assert
		options.BatchSize.ShouldBeGreaterThan(100);
		options.PublishIntervalMs.ShouldBeLessThan(1000);
	}

	[Fact]
	public void Options_ForLightMode_HasInMemoryStorage()
	{
		// Act
		var options = new OutboxOptions
		{
			UseInMemoryStorage = true,
			Enabled = true,
		};

		// Assert
		options.UseInMemoryStorage.ShouldBeTrue();
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForReliableDelivery_HasHighRetryCount()
	{
		// Act
		var options = new OutboxOptions
		{
			MaxRetries = 10,
			SentMessageRetention = TimeSpan.FromDays(7),
		};

		// Assert
		options.MaxRetries.ShouldBeGreaterThan(3);
		options.SentMessageRetention.ShouldBeGreaterThan(TimeSpan.FromDays(1));
	}

	#endregion
}
