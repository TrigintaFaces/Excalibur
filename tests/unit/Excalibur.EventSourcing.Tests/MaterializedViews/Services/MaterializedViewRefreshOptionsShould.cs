// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Services;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Services;

/// <summary>
/// Unit tests for <see cref="MaterializedViewRefreshOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 517: Materialized Views provider tests.
/// Tests verify options defaults and property setters for refresh service configuration.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Services")]
public sealed class MaterializedViewRefreshOptionsShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(MaterializedViewRefreshOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(MaterializedViewRefreshOptions).IsPublic.ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveCatchUpOnStartupDisabledByDefault()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.CatchUpOnStartup.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultRefreshIntervalOfThirtySeconds()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveNullCronExpressionByDefault()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.CronExpression.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultBatchSizeOfOneHundred()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultInitialRetryDelayOfOneSecond()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultMaxRetryDelayOfFiveMinutes()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultMaxRetryCountOfFive()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}

	[Fact]
	public void BeEnabledByDefault()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowEnablingCatchUpOnStartup()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			CatchUpOnStartup = true
		};

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingRefreshInterval()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			RefreshInterval = TimeSpan.FromMinutes(5)
		};

		// Assert
		options.RefreshInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowSettingRefreshIntervalToNull()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			RefreshInterval = null
		};

		// Assert
		options.RefreshInterval.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCronExpression()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			CronExpression = "*/5 * * * *"
		};

		// Assert
		options.CronExpression.ShouldBe("*/5 * * * *");
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			BatchSize = 500
		};

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingInitialRetryDelay()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			InitialRetryDelay = TimeSpan.FromMilliseconds(500)
		};

		// Assert
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowSettingMaxRetryDelay()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			MaxRetryDelay = TimeSpan.FromMinutes(10)
		};

		// Assert
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowSettingMaxRetryCount()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			MaxRetryCount = 10
		};

		// Assert
		options.MaxRetryCount.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingMaxRetryCountToZeroForInfiniteRetries()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			MaxRetryCount = 0
		};

		// Assert
		options.MaxRetryCount.ShouldBe(0);
	}

	[Fact]
	public void AllowDisablingService()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			Enabled = false
		};

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	#endregion

	#region Complex Configuration Tests

	[Fact]
	public void SupportFullConfiguration()
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			CatchUpOnStartup = true,
			RefreshInterval = TimeSpan.FromMinutes(2),
			CronExpression = "0 * * * *",
			BatchSize = 250,
			InitialRetryDelay = TimeSpan.FromMilliseconds(500),
			MaxRetryDelay = TimeSpan.FromMinutes(10),
			MaxRetryCount = 3,
			Enabled = true
		};

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
		options.RefreshInterval.ShouldBe(TimeSpan.FromMinutes(2));
		options.CronExpression.ShouldBe("0 * * * *");
		options.BatchSize.ShouldBe(250);
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.MaxRetryCount.ShouldBe(3);
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void SupportCronOnlyConfiguration()
	{
		// Act - disable interval-based scheduling, use cron only
		var options = new MaterializedViewRefreshOptions
		{
			RefreshInterval = null,
			CronExpression = "0 0 * * *" // Daily at midnight
		};

		// Assert
		options.RefreshInterval.ShouldBeNull();
		options.CronExpression.ShouldBe("0 0 * * *");
	}

	[Fact]
	public void SupportIntervalOnlyConfiguration()
	{
		// Act - explicit interval, no cron
		var options = new MaterializedViewRefreshOptions
		{
			RefreshInterval = TimeSpan.FromMinutes(15),
			CronExpression = null
		};

		// Assert
		options.RefreshInterval.ShouldBe(TimeSpan.FromMinutes(15));
		options.CronExpression.ShouldBeNull();
	}

	#endregion

	#region Cron Expression Examples

	[Theory]
	[InlineData("* * * * *", "Every minute")]
	[InlineData("*/5 * * * *", "Every 5 minutes")]
	[InlineData("0 * * * *", "Every hour at minute 0")]
	[InlineData("0 0 * * *", "Daily at midnight")]
	[InlineData("0 2 * * 0", "Weekly on Sunday at 2 AM")]
	public void AcceptVariousCronExpressions(string cronExpression, string description)
	{
		// Act
		var options = new MaterializedViewRefreshOptions
		{
			CronExpression = cronExpression
		};

		// Assert
		options.CronExpression.ShouldBe(cronExpression);
		description.ShouldNotBeNullOrEmpty(); // Use description to avoid warning
	}

	#endregion
}
