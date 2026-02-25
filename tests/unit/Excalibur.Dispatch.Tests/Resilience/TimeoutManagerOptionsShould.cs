// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="TimeoutManagerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class TimeoutManagerOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void DefaultDatabaseTimeout_IsFifteenSeconds()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.DatabaseTimeout.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void DefaultHttpTimeout_IsHundredSeconds()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(100));
	}

	[Fact]
	public void DefaultMessageQueueTimeout_IsSixtySeconds()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.MessageQueueTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void DefaultCacheTimeout_IsFiveSeconds()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.CacheTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void DefaultLogTimeoutWarnings_IsTrue()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.LogTimeoutWarnings.ShouldBeTrue();
	}

	[Fact]
	public void DefaultSlowOperationThreshold_IsPointEight()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.SlowOperationThreshold.ShouldBe(0.8);
	}

	[Fact]
	public void OperationTimeouts_IsInitializedEmpty()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.OperationTimeouts.ShouldNotBeNull();
		options.OperationTimeouts.Count.ShouldBe(0);
	}

	[Fact]
	public void OperationTimeouts_CanAddEntries()
	{
		// Arrange
		var options = new TimeoutManagerOptions();

		// Act
		options.OperationTimeouts["Custom.Op"] = TimeSpan.FromSeconds(42);

		// Assert
		options.OperationTimeouts.Count.ShouldBe(1);
		options.OperationTimeouts["Custom.Op"].ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void AllProperties_CanBeCustomized()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(60),
			DatabaseTimeout = TimeSpan.FromSeconds(30),
			HttpTimeout = TimeSpan.FromSeconds(200),
			MessageQueueTimeout = TimeSpan.FromSeconds(120),
			CacheTimeout = TimeSpan.FromSeconds(10),
			LogTimeoutWarnings = false,
			SlowOperationThreshold = 0.5,
		};

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.DatabaseTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(200));
		options.MessageQueueTimeout.ShouldBe(TimeSpan.FromSeconds(120));
		options.CacheTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.LogTimeoutWarnings.ShouldBeFalse();
		options.SlowOperationThreshold.ShouldBe(0.5);
	}
}
