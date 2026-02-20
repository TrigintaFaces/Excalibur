// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="TimeoutManagerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class TimeoutManagerOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new TimeoutManagerOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.DatabaseTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(100));
		options.MessageQueueTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.CacheTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.LogTimeoutWarnings.ShouldBeTrue();
		options.SlowOperationThreshold.ShouldBe(0.8);
		options.OperationTimeouts.ShouldNotBeNull();
		options.OperationTimeouts.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultTimeout_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { DefaultTimeout = TimeSpan.FromSeconds(60) };

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void DatabaseTimeout_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { DatabaseTimeout = TimeSpan.FromSeconds(30) };

		// Assert
		options.DatabaseTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HttpTimeout_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { HttpTimeout = TimeSpan.FromSeconds(120) };

		// Assert
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(120));
	}

	[Fact]
	public void MessageQueueTimeout_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { MessageQueueTimeout = TimeSpan.FromSeconds(90) };

		// Assert
		options.MessageQueueTimeout.ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Fact]
	public void CacheTimeout_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { CacheTimeout = TimeSpan.FromSeconds(10) };

		// Assert
		options.CacheTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void LogTimeoutWarnings_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { LogTimeoutWarnings = false };

		// Assert
		options.LogTimeoutWarnings.ShouldBeFalse();
	}

	[Fact]
	public void SlowOperationThreshold_CanBeSet()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions { SlowOperationThreshold = 0.9 };

		// Assert
		options.SlowOperationThreshold.ShouldBe(0.9);
	}

	[Fact]
	public void OperationTimeouts_CanAddEntries()
	{
		// Arrange
		var options = new TimeoutManagerOptions();

		// Act
		options.OperationTimeouts["CustomOperation"] = TimeSpan.FromSeconds(45);

		// Assert
		options.OperationTimeouts.Count.ShouldBe(1);
		options.OperationTimeouts["CustomOperation"].ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void OperationTimeouts_CanAddMultipleEntries()
	{
		// Arrange
		var options = new TimeoutManagerOptions();

		// Act
		options.OperationTimeouts["Op1"] = TimeSpan.FromSeconds(10);
		options.OperationTimeouts["Op2"] = TimeSpan.FromSeconds(20);
		options.OperationTimeouts["Op3"] = TimeSpan.FromSeconds(30);

		// Assert
		options.OperationTimeouts.Count.ShouldBe(3);
	}

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange & Act
		var options = new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(45),
			DatabaseTimeout = TimeSpan.FromSeconds(20),
			HttpTimeout = TimeSpan.FromSeconds(150),
			MessageQueueTimeout = TimeSpan.FromSeconds(75),
			CacheTimeout = TimeSpan.FromSeconds(3),
			LogTimeoutWarnings = false,
			SlowOperationThreshold = 0.75
		};
		options.OperationTimeouts["Custom"] = TimeSpan.FromSeconds(100);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(45));
		options.DatabaseTimeout.ShouldBe(TimeSpan.FromSeconds(20));
		options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(150));
		options.MessageQueueTimeout.ShouldBe(TimeSpan.FromSeconds(75));
		options.CacheTimeout.ShouldBe(TimeSpan.FromSeconds(3));
		options.LogTimeoutWarnings.ShouldBeFalse();
		options.SlowOperationThreshold.ShouldBe(0.75);
		options.OperationTimeouts["Custom"].ShouldBe(TimeSpan.FromSeconds(100));
	}
}
