// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Watch;

namespace Excalibur.LeaderElection.Tests.Watch;

/// <summary>
/// Unit tests for <see cref="LeaderWatchOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LeaderWatchOptionsShould
{
	[Fact]
	public void HaveCorrectDefaultPollInterval()
	{
		// Arrange & Act
		var options = new LeaderWatchOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveCorrectDefaultIncludeHeartbeats()
	{
		// Arrange & Act
		var options = new LeaderWatchOptions();

		// Assert
		options.IncludeHeartbeats.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingPollInterval()
	{
		// Arrange & Act
		var options = new LeaderWatchOptions
		{
			PollInterval = TimeSpan.FromSeconds(30),
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingIncludeHeartbeats()
	{
		// Arrange & Act
		var options = new LeaderWatchOptions
		{
			IncludeHeartbeats = true,
		};

		// Assert
		options.IncludeHeartbeats.ShouldBeTrue();
	}

	[Fact]
	public void AllowMinimumPollInterval()
	{
		// Arrange & Act
		var options = new LeaderWatchOptions
		{
			PollInterval = TimeSpan.FromSeconds(1),
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AllowMaximumPollInterval()
	{
		// Arrange & Act
		var options = new LeaderWatchOptions
		{
			PollInterval = TimeSpan.FromHours(1),
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromHours(1));
	}
}
