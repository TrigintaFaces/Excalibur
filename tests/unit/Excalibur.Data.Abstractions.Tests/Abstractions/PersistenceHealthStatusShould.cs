// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="PersistenceHealthStatus"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class PersistenceHealthStatusShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var status = new PersistenceHealthStatus();

		// Assert
		status.ProviderName.ShouldBe(string.Empty);
		status.IsHealthy.ShouldBeFalse();
		status.Message.ShouldBe(string.Empty);
		status.ResponseTimeMs.ShouldBeNull();
		status.CheckedAt.ShouldNotBe(default);
		status.Data.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingProviderName()
	{
		// Act
		var status = new PersistenceHealthStatus
		{
			ProviderName = "SqlServer"
		};

		// Assert
		status.ProviderName.ShouldBe("SqlServer");
	}

	[Fact]
	public void AllowSettingHealthyStatus()
	{
		// Act
		var status = new PersistenceHealthStatus
		{
			IsHealthy = true
		};

		// Assert
		status.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingMessage()
	{
		// Act
		var status = new PersistenceHealthStatus
		{
			Message = "Connection successful"
		};

		// Assert
		status.Message.ShouldBe("Connection successful");
	}

	[Fact]
	public void AllowSettingResponseTime()
	{
		// Act
		var status = new PersistenceHealthStatus
		{
			ResponseTimeMs = 150
		};

		// Assert
		status.ResponseTimeMs.ShouldBe(150);
	}

	[Fact]
	public void AllowSettingCheckedAt()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var status = new PersistenceHealthStatus
		{
			CheckedAt = timestamp
		};

		// Assert
		status.CheckedAt.ShouldBe(timestamp);
	}

	[Fact]
	public void SetCheckedAtToCurrentTimeByDefault()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var status = new PersistenceHealthStatus();

		// Assert
		var after = DateTimeOffset.UtcNow;
		status.CheckedAt.ShouldBeGreaterThanOrEqualTo(before);
		status.CheckedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void CreateHealthyStatus()
	{
		// Act
		var status = new PersistenceHealthStatus
		{
			ProviderName = "Postgres",
			IsHealthy = true,
			Message = "Database connection verified",
			ResponseTimeMs = 25
		};

		// Assert
		status.IsHealthy.ShouldBeTrue();
		status.ProviderName.ShouldBe("Postgres");
		status.Message.ShouldBe("Database connection verified");
		status.ResponseTimeMs.ShouldBe(25);
	}

	[Fact]
	public void CreateUnhealthyStatus()
	{
		// Act
		var status = new PersistenceHealthStatus
		{
			ProviderName = "MongoDB",
			IsHealthy = false,
			Message = "Connection refused",
			ResponseTimeMs = null
		};

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ProviderName.ShouldBe("MongoDB");
		status.Message.ShouldBe("Connection refused");
		status.ResponseTimeMs.ShouldBeNull();
	}
}
