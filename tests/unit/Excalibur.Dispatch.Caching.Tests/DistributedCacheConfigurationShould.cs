// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="DistributedCacheConfiguration"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DistributedCacheConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultKeyPrefix()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration();

		// Assert
		config.KeyPrefix.ShouldBe("dispatch:");
	}

	[Fact]
	public void HaveFalseUseBinarySerialization_ByDefault()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration();

		// Assert
		config.UseBinarySerialization.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration();

		// Assert
		config.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRetryDelay()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration();

		// Assert
		config.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingKeyPrefix()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.KeyPrefix = "myapp:cache:";

		// Assert
		config.KeyPrefix.ShouldBe("myapp:cache:");
	}

	[Fact]
	public void AllowSettingUseBinarySerialization()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.UseBinarySerialization = true;

		// Assert
		config.UseBinarySerialization.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMaxRetryAttempts()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.MaxRetryAttempts = 5;

		// Assert
		config.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingRetryDelay()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.RetryDelay = TimeSpan.FromSeconds(1);

		// Assert
		config.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration
		{
			KeyPrefix = "custom:",
			UseBinarySerialization = true,
			MaxRetryAttempts = 10,
			RetryDelay = TimeSpan.FromMilliseconds(500),
		};

		// Assert
		config.KeyPrefix.ShouldBe("custom:");
		config.UseBinarySerialization.ShouldBeTrue();
		config.MaxRetryAttempts.ShouldBe(10);
		config.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowZeroMaxRetryAttempts()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.MaxRetryAttempts = 0;

		// Assert
		config.MaxRetryAttempts.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroRetryDelay()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.RetryDelay = TimeSpan.Zero;

		// Assert
		config.RetryDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowEmptyKeyPrefix()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.KeyPrefix = string.Empty;

		// Assert
		config.KeyPrefix.ShouldBe(string.Empty);
	}

	#endregion
}
