// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for DistributedCacheConfiguration POCO.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DistributedCacheConfigurationShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration();

		// Assert
		config.KeyPrefix.ShouldBe("dispatch:");
		config.UseBinarySerialization.ShouldBeFalse();
		config.MaxRetryAttempts.ShouldBe(3);
		config.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Set_KeyPrefix_StoresValue()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.KeyPrefix = "custom:";

		// Assert
		config.KeyPrefix.ShouldBe("custom:");
	}

	[Fact]
	public void Set_UseBinarySerialization_StoresValue()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.UseBinarySerialization = true;

		// Assert
		config.UseBinarySerialization.ShouldBeTrue();
	}

	[Fact]
	public void Set_MaxRetryAttempts_StoresValue()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.MaxRetryAttempts = 5;

		// Assert
		config.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void Set_RetryDelay_StoresValue()
	{
		// Arrange
		var config = new DistributedCacheConfiguration();

		// Act
		config.RetryDelay = TimeSpan.FromMilliseconds(200);

		// Assert
		config.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

	[Fact]
	public void Create_WithAllProperties_StoresAllValues()
	{
		// Arrange & Act
		var config = new DistributedCacheConfiguration
		{
			KeyPrefix = "app:",
			UseBinarySerialization = true,
			MaxRetryAttempts = 10,
			RetryDelay = TimeSpan.FromSeconds(1)
		};

		// Assert
		config.KeyPrefix.ShouldBe("app:");
		config.UseBinarySerialization.ShouldBeTrue();
		config.MaxRetryAttempts.ShouldBe(10);
		config.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}
}
