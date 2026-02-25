// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="ChannelMessagePumpStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class ChannelMessagePumpStatusShould
{
	#region Value Tests

	[Fact]
	public void NotStarted_HasValueZero()
	{
		// Assert
		((int)ChannelMessagePumpStatus.NotStarted).ShouldBe(0);
	}

	[Fact]
	public void Starting_HasValueOne()
	{
		// Assert
		((int)ChannelMessagePumpStatus.Starting).ShouldBe(1);
	}

	[Fact]
	public void Running_HasValueTwo()
	{
		// Assert
		((int)ChannelMessagePumpStatus.Running).ShouldBe(2);
	}

	[Fact]
	public void Stopping_HasValueThree()
	{
		// Assert
		((int)ChannelMessagePumpStatus.Stopping).ShouldBe(3);
	}

	[Fact]
	public void Stopped_HasValueFour()
	{
		// Assert
		((int)ChannelMessagePumpStatus.Stopped).ShouldBe(4);
	}

	[Fact]
	public void Faulted_HasValueFive()
	{
		// Assert
		((int)ChannelMessagePumpStatus.Faulted).ShouldBe(5);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<ChannelMessagePumpStatus>();

		// Assert
		values.Length.ShouldBe(6);
	}

	[Theory]
	[InlineData(ChannelMessagePumpStatus.NotStarted)]
	[InlineData(ChannelMessagePumpStatus.Starting)]
	[InlineData(ChannelMessagePumpStatus.Running)]
	[InlineData(ChannelMessagePumpStatus.Stopping)]
	[InlineData(ChannelMessagePumpStatus.Stopped)]
	[InlineData(ChannelMessagePumpStatus.Faulted)]
	public void AllValues_AreDefined(ChannelMessagePumpStatus status)
	{
		// Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(ChannelMessagePumpStatus.NotStarted, "NotStarted")]
	[InlineData(ChannelMessagePumpStatus.Starting, "Starting")]
	[InlineData(ChannelMessagePumpStatus.Running, "Running")]
	[InlineData(ChannelMessagePumpStatus.Stopping, "Stopping")]
	[InlineData(ChannelMessagePumpStatus.Stopped, "Stopped")]
	[InlineData(ChannelMessagePumpStatus.Faulted, "Faulted")]
	public void ToString_ReturnsExpectedValue(ChannelMessagePumpStatus status, string expected)
	{
		// Assert
		status.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("NotStarted", ChannelMessagePumpStatus.NotStarted)]
	[InlineData("Starting", ChannelMessagePumpStatus.Starting)]
	[InlineData("Running", ChannelMessagePumpStatus.Running)]
	[InlineData("Stopping", ChannelMessagePumpStatus.Stopping)]
	[InlineData("Stopped", ChannelMessagePumpStatus.Stopped)]
	[InlineData("Faulted", ChannelMessagePumpStatus.Faulted)]
	public void Parse_ReturnsExpectedValue(string input, ChannelMessagePumpStatus expected)
	{
		// Act
		var result = Enum.Parse<ChannelMessagePumpStatus>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<ChannelMessagePumpStatus>("running", ignoreCase: true);

		// Assert
		result.ShouldBe(ChannelMessagePumpStatus.Running);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNotStarted()
	{
		// Arrange
		var defaultValue = default(ChannelMessagePumpStatus);

		// Assert
		defaultValue.ShouldBe(ChannelMessagePumpStatus.NotStarted);
	}

	#endregion

	#region Lifecycle Order Tests

	[Fact]
	public void StatusValues_AreInLogicalOrder()
	{
		// Assert - statuses follow a logical lifecycle progression
		((int)ChannelMessagePumpStatus.NotStarted).ShouldBeLessThan((int)ChannelMessagePumpStatus.Starting);
		((int)ChannelMessagePumpStatus.Starting).ShouldBeLessThan((int)ChannelMessagePumpStatus.Running);
		((int)ChannelMessagePumpStatus.Running).ShouldBeLessThan((int)ChannelMessagePumpStatus.Stopping);
		((int)ChannelMessagePumpStatus.Stopping).ShouldBeLessThan((int)ChannelMessagePumpStatus.Stopped);
	}

	#endregion
}
