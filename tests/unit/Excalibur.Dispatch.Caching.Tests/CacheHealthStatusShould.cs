// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheHealthStatus"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "Performance")]
public sealed class CacheHealthStatusShould : UnitTestBase
{
	[Fact]
	public void HaveFalseIsHealthy_ByDefault()
	{
		// Arrange & Act
		var status = new CacheHealthStatus();

		// Assert
		status.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void HaveZeroResponseTimeMs_ByDefault()
	{
		// Arrange & Act
		var status = new CacheHealthStatus();

		// Assert
		status.ResponseTimeMs.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyConnectionStatus_ByDefault()
	{
		// Arrange & Act
		var status = new CacheHealthStatus();

		// Assert
		status.ConnectionStatus.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultLastChecked()
	{
		// Arrange & Act
		var status = new CacheHealthStatus();

		// Assert
		status.LastChecked.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AllowSettingIsHealthy()
	{
		// Act
		var status = new CacheHealthStatus
		{
			IsHealthy = true
		};

		// Assert
		status.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingResponseTimeMs()
	{
		// Act
		var status = new CacheHealthStatus
		{
			ResponseTimeMs = 15.5
		};

		// Assert
		status.ResponseTimeMs.ShouldBe(15.5);
	}

	[Fact]
	public void AllowSettingConnectionStatus()
	{
		// Act
		var status = new CacheHealthStatus
		{
			ConnectionStatus = "Connected"
		};

		// Assert
		status.ConnectionStatus.ShouldBe("Connected");
	}

	[Fact]
	public void AllowSettingLastChecked()
	{
		// Arrange
		var lastChecked = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var status = new CacheHealthStatus
		{
			LastChecked = lastChecked
		};

		// Assert
		status.LastChecked.ShouldBe(lastChecked);
	}

	[Fact]
	public void InitializeWithAllProperties()
	{
		// Arrange
		var lastChecked = DateTimeOffset.UtcNow;

		// Act
		var status = new CacheHealthStatus
		{
			IsHealthy = true,
			ResponseTimeMs = 10.5,
			ConnectionStatus = "Healthy - Redis connected",
			LastChecked = lastChecked
		};

		// Assert
		status.IsHealthy.ShouldBeTrue();
		status.ResponseTimeMs.ShouldBe(10.5);
		status.ConnectionStatus.ShouldBe("Healthy - Redis connected");
		status.LastChecked.ShouldBe(lastChecked);
	}
}
