// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience.Hedging;

/// <summary>
/// Unit tests for <see cref="HedgingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HedgingOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new HedgingOptions();

		// Assert
		options.MaxHedgedAttempts.ShouldBe(2);
		options.Delay.ShouldBe(TimeSpan.FromSeconds(2));
		options.EnableDetailedLogging.ShouldBeTrue();
		options.ShouldHedge.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMaxHedgedAttempts()
	{
		// Arrange & Act
		var options = new HedgingOptions { MaxHedgedAttempts = 5 };

		// Assert
		options.MaxHedgedAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingDelay()
	{
		// Arrange & Act
		var options = new HedgingOptions { Delay = TimeSpan.FromMilliseconds(500) };

		// Assert
		options.Delay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowDisablingDetailedLogging()
	{
		// Arrange & Act
		var options = new HedgingOptions { EnableDetailedLogging = false };

		// Assert
		options.EnableDetailedLogging.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingShouldHedgePredicate()
	{
		// Arrange
		Func<Exception, bool> predicate = ex => ex is TimeoutException;

		// Act
		var options = new HedgingOptions { ShouldHedge = predicate };

		// Assert
		options.ShouldHedge.ShouldNotBeNull();
		options.ShouldHedge(new TimeoutException()).ShouldBeTrue();
		options.ShouldHedge(new InvalidOperationException()).ShouldBeFalse();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void AcceptValidMaxHedgedAttempts(int attempts)
	{
		// Arrange & Act
		var options = new HedgingOptions { MaxHedgedAttempts = attempts };

		// Assert
		options.MaxHedgedAttempts.ShouldBe(attempts);
	}
}
