// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Application.Requests;

/// <summary>
/// Unit tests for <see cref="ActivityType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Requests")]
public sealed class ActivityTypeShould : UnitTestBase
{
	[Fact]
	public void HaveFiveActivityTypes()
	{
		// Act
		var values = Enum.GetValues<ActivityType>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void HaveUnknownAsDefault()
	{
		// Assert
		ActivityType defaultValue = default;
		defaultValue.ShouldBe(ActivityType.Unknown);
	}

	[Theory]
	[InlineData(ActivityType.Unknown, 0)]
	[InlineData(ActivityType.Command, 1)]
	[InlineData(ActivityType.Query, 2)]
	[InlineData(ActivityType.Notification, 3)]
	[InlineData(ActivityType.Job, 4)]
	public void HaveCorrectUnderlyingValues(ActivityType activityType, int expectedValue)
	{
		// Assert
		((int)activityType).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Unknown", ActivityType.Unknown)]
	[InlineData("Command", ActivityType.Command)]
	[InlineData("Query", ActivityType.Query)]
	[InlineData("Notification", ActivityType.Notification)]
	[InlineData("Job", ActivityType.Job)]
	public void ParseFromString(string input, ActivityType expected)
	{
		// Act
		var result = Enum.Parse<ActivityType>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Act & Assert
		foreach (var activityType in Enum.GetValues<ActivityType>())
		{
			Enum.IsDefined(activityType).ShouldBeTrue();
		}
	}
}
