// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="OutboxOptions"/> in the Middleware namespace.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MiddlewareOutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_DefaultPriority_IsZero()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.DefaultPriority.ShouldBe(0);
	}

	[Fact]
	public void Default_ContinueOnStagingError_IsFalse()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.ContinueOnStagingError.ShouldBeFalse();
	}

	[Fact]
	public void Default_BypassOutboxForTypes_IsNull()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.BypassOutboxForTypes.ShouldBeNull();
	}

	[Fact]
	public void Default_PublishBatchSize_Is100()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.PublishBatchSize.ShouldBe(100);
	}

	[Fact]
	public void Default_PublishPollingInterval_Is5Seconds()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.PublishPollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Default_MaxRetries_Is3()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void Default_RetryDelay_Is5Minutes()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_CleanupAge_Is7Days()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.CleanupAge.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void Default_CleanupInterval_Is1Hour()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultPriority_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.DefaultPriority = 5;

		// Assert
		options.DefaultPriority.ShouldBe(5);
	}

	[Fact]
	public void ContinueOnStagingError_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.ContinueOnStagingError = true;

		// Assert
		options.ContinueOnStagingError.ShouldBeTrue();
	}

	[Fact]
	public void BypassOutboxForTypes_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.BypassOutboxForTypes = ["MyNamespace.MyMessage"];

		// Assert
		_ = options.BypassOutboxForTypes.ShouldNotBeNull();
		options.BypassOutboxForTypes.Length.ShouldBe(1);
		options.BypassOutboxForTypes[0].ShouldBe("MyNamespace.MyMessage");
	}

	[Fact]
	public void PublishBatchSize_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.PublishBatchSize = 500;

		// Assert
		options.PublishBatchSize.ShouldBe(500);
	}

	[Fact]
	public void PublishPollingInterval_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.PublishPollingInterval = TimeSpan.FromSeconds(10);

		// Assert
		options.PublishPollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void MaxRetries_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.MaxRetries = 5;

		// Assert
		options.MaxRetries.ShouldBe(5);
	}

	[Fact]
	public void RetryDelay_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.RetryDelay = TimeSpan.FromMinutes(10);

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void CleanupAge_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.CleanupAge = TimeSpan.FromDays(30);

		// Assert
		options.CleanupAge.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void CleanupInterval_CanBeSet()
	{
		// Arrange
		var options = new OutboxOptions();

		// Act
		options.CleanupInterval = TimeSpan.FromHours(6);

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new OutboxOptions
		{
			Enabled = true,
			DefaultPriority = 10,
			ContinueOnStagingError = true,
			BypassOutboxForTypes = ["Type1", "Type2"],
			PublishBatchSize = 200,
			PublishPollingInterval = TimeSpan.FromSeconds(2),
			MaxRetries = 5,
			RetryDelay = TimeSpan.FromMinutes(10),
			CleanupAge = TimeSpan.FromDays(14),
			CleanupInterval = TimeSpan.FromHours(4),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultPriority.ShouldBe(10);
		options.ContinueOnStagingError.ShouldBeTrue();
		_ = options.BypassOutboxForTypes.ShouldNotBeNull();
		options.BypassOutboxForTypes.Length.ShouldBe(2);
		options.PublishBatchSize.ShouldBe(200);
		options.PublishPollingInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxRetries.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.CleanupAge.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(4));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeBatchSize()
	{
		// Act
		var options = new OutboxOptions
		{
			Enabled = true,
			PublishBatchSize = 1000,
			PublishPollingInterval = TimeSpan.FromSeconds(1),
		};

		// Assert
		options.PublishBatchSize.ShouldBeGreaterThan(100);
		options.PublishPollingInterval.ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Options_ForReliability_EnablesRetries()
	{
		// Act
		var options = new OutboxOptions
		{
			Enabled = true,
			MaxRetries = 10,
			RetryDelay = TimeSpan.FromMinutes(1),
			ContinueOnStagingError = false,
		};

		// Assert
		options.MaxRetries.ShouldBeGreaterThan(3);
		options.ContinueOnStagingError.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForLongRetention_HasExtendedCleanupAge()
	{
		// Act
		var options = new OutboxOptions
		{
			Enabled = true,
			CleanupAge = TimeSpan.FromDays(30),
			CleanupInterval = TimeSpan.FromHours(12),
		};

		// Assert
		options.CleanupAge.ShouldBeGreaterThan(TimeSpan.FromDays(7));
	}

	#endregion
}
