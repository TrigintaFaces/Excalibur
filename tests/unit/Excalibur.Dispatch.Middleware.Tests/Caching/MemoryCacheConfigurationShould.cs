// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for MemoryCacheConfiguration POCO.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MemoryCacheConfigurationShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var config = new MemoryCacheConfiguration();

		// Assert
		config.SizeLimit.ShouldBeNull();
		config.CompactionPercentage.ShouldBe(0.05);
		config.ExpirationScanFrequency.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Set_SizeLimit_StoresValue()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.SizeLimit = 1024;

		// Assert
		config.SizeLimit.ShouldBe(1024);
	}

	[Fact]
	public void Set_CompactionPercentage_StoresValue()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.CompactionPercentage = 0.10;

		// Assert
		config.CompactionPercentage.ShouldBe(0.10);
	}

	[Fact]
	public void Set_ExpirationScanFrequency_StoresValue()
	{
		// Arrange
		var config = new MemoryCacheConfiguration();

		// Act
		config.ExpirationScanFrequency = TimeSpan.FromSeconds(30);

		// Assert
		config.ExpirationScanFrequency.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Create_WithAllProperties_StoresAllValues()
	{
		// Arrange & Act
		var config = new MemoryCacheConfiguration
		{
			SizeLimit = 2048,
			CompactionPercentage = 0.15,
			ExpirationScanFrequency = TimeSpan.FromMinutes(5)
		};

		// Assert
		config.SizeLimit.ShouldBe(2048);
		config.CompactionPercentage.ShouldBe(0.15);
		config.ExpirationScanFrequency.ShouldBe(TimeSpan.FromMinutes(5));
	}
}
