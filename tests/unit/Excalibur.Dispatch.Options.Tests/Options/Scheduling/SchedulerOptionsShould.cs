// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

namespace Excalibur.Dispatch.Tests.Options.Scheduling;

/// <summary>
/// Unit tests for <see cref="SchedulerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class SchedulerOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_PollInterval_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new SchedulerOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_PastScheduleBehavior_IsExecuteImmediately()
	{
		// Arrange & Act
		var options = new SchedulerOptions();

		// Assert
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.ExecuteImmediately);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void PollInterval_CanBeSet()
	{
		// Arrange
		var options = new SchedulerOptions();

		// Act
		options.PollInterval = TimeSpan.FromMinutes(5);

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void PastScheduleBehavior_CanBeSet()
	{
		// Arrange
		var options = new SchedulerOptions();

		// Act
		options.PastScheduleBehavior = PastScheduleBehavior.Reject;

		// Assert
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.Reject);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new SchedulerOptions
		{
			PollInterval = TimeSpan.FromSeconds(10),
			PastScheduleBehavior = PastScheduleBehavior.Reject,
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.Reject);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForFrequentPolling_HasShortInterval()
	{
		// Act
		var options = new SchedulerOptions
		{
			PollInterval = TimeSpan.FromSeconds(1),
		};

		// Assert
		options.PollInterval.ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void Options_ForStrictScheduling_RejectsPastSchedules()
	{
		// Act
		var options = new SchedulerOptions
		{
			PastScheduleBehavior = PastScheduleBehavior.Reject,
		};

		// Assert
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.Reject);
	}

	#endregion
}
