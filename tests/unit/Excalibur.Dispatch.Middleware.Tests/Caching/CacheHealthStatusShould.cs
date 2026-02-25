// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for CacheHealthStatus POCO.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheHealthStatusShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var status = new CacheHealthStatus();

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ResponseTimeMs.ShouldBe(0);
		status.ConnectionStatus.ShouldBe(string.Empty);
		status.LastChecked.ShouldBe(default);
	}

	[Fact]
	public void Create_WithInitializer_StoresValues()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var status = new CacheHealthStatus
		{
			IsHealthy = true,
			ResponseTimeMs = 5.3,
			ConnectionStatus = "Connected",
			LastChecked = timestamp
		};

		// Assert
		status.IsHealthy.ShouldBeTrue();
		status.ResponseTimeMs.ShouldBe(5.3);
		status.ConnectionStatus.ShouldBe("Connected");
		status.LastChecked.ShouldBe(timestamp);
	}

	[Fact]
	public void Create_WithUnhealthyStatus_StoresCorrectly()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var status = new CacheHealthStatus
		{
			IsHealthy = false,
			ResponseTimeMs = 0,
			ConnectionStatus = "Redis not available",
			LastChecked = timestamp
		};

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ResponseTimeMs.ShouldBe(0);
		status.ConnectionStatus.ShouldBe("Redis not available");
		status.LastChecked.ShouldBe(timestamp);
	}

	[Fact]
	public void Create_WithErrorStatus_StoresErrorMessage()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var errorMessage = "Error: Connection timeout";

		// Act
		var status = new CacheHealthStatus
		{
			IsHealthy = false,
			ResponseTimeMs = 0,
			ConnectionStatus = errorMessage,
			LastChecked = timestamp
		};

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldBe(errorMessage);
		status.ConnectionStatus.ShouldContain("Error:");
		status.ConnectionStatus.ShouldContain("timeout");
	}
}
