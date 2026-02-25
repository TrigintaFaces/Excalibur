// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="EventStoreDispatcherOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class EventStoreDispatcherOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_PollInterval_Is15Seconds()
	{
		// Arrange & Act
		var options = new EventStoreDispatcherOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(15));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void PollInterval_CanBeSet()
	{
		// Arrange
		var options = new EventStoreDispatcherOptions();

		// Act
		options.PollInterval = TimeSpan.FromSeconds(30);

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsPollInterval()
	{
		// Act
		var options = new EventStoreDispatcherOptions
		{
			PollInterval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighFrequency_HasShortPollInterval()
	{
		// Act
		var options = new EventStoreDispatcherOptions
		{
			PollInterval = TimeSpan.FromSeconds(5),
		};

		// Assert
		options.PollInterval.ShouldBeLessThan(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void Options_ForLowFrequency_HasLongPollInterval()
	{
		// Act
		var options = new EventStoreDispatcherOptions
		{
			PollInterval = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.PollInterval.ShouldBeGreaterThan(TimeSpan.FromSeconds(15));
	}

	#endregion
}
