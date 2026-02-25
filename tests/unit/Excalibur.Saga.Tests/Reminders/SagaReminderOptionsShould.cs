// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Reminders;

namespace Excalibur.Saga.Tests.Reminders;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaReminderOptionsShould
{
	[Fact]
	public void DefaultDelayToFiveMinutes()
	{
		// Arrange & Act
		var options = new SagaReminderOptions();

		// Assert
		options.DefaultDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void DefaultMaxRemindersPerSagaToTen()
	{
		// Arrange & Act
		var options = new SagaReminderOptions();

		// Assert
		options.MaxRemindersPerSaga.ShouldBe(10);
	}

	[Fact]
	public void DefaultMinimumDelayToOneSecond()
	{
		// Arrange & Act
		var options = new SagaReminderOptions();

		// Assert
		options.MinimumDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void DefaultMaximumDelayToThirtyDays()
	{
		// Arrange & Act
		var options = new SagaReminderOptions();

		// Assert
		options.MaximumDelay.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void AllowSettingDefaultDelay()
	{
		// Arrange
		var options = new SagaReminderOptions();

		// Act
		options.DefaultDelay = TimeSpan.FromMinutes(10);

		// Assert
		options.DefaultDelay.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowSettingMaxRemindersPerSaga()
	{
		// Arrange
		var options = new SagaReminderOptions();

		// Act
		options.MaxRemindersPerSaga = 50;

		// Assert
		options.MaxRemindersPerSaga.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingMinimumDelay()
	{
		// Arrange
		var options = new SagaReminderOptions();

		// Act
		options.MinimumDelay = TimeSpan.FromMilliseconds(500);

		// Assert
		options.MinimumDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowSettingMaximumDelay()
	{
		// Arrange
		var options = new SagaReminderOptions();

		// Act
		options.MaximumDelay = TimeSpan.FromDays(60);

		// Assert
		options.MaximumDelay.ShouldBe(TimeSpan.FromDays(60));
	}
}
