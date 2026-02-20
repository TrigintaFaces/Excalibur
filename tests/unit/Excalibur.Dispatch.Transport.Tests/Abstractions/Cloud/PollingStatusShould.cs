// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

/// <summary>
/// Unit tests for <see cref="PollingStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class PollingStatusShould
{
	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<PollingStatus>();

		// Assert
		values.Length.ShouldBe(5);
		values.ShouldContain(PollingStatus.Idle);
		values.ShouldContain(PollingStatus.Running);
		values.ShouldContain(PollingStatus.Paused);
		values.ShouldContain(PollingStatus.Stopped);
		values.ShouldContain(PollingStatus.Error);
	}

	[Fact]
	public void Idle_HasExpectedValue()
	{
		// Assert
		((int)PollingStatus.Idle).ShouldBe(0);
	}

	[Fact]
	public void Running_HasExpectedValue()
	{
		// Assert
		((int)PollingStatus.Running).ShouldBe(1);
	}

	[Fact]
	public void Paused_HasExpectedValue()
	{
		// Assert
		((int)PollingStatus.Paused).ShouldBe(2);
	}

	[Fact]
	public void Stopped_HasExpectedValue()
	{
		// Assert
		((int)PollingStatus.Stopped).ShouldBe(3);
	}

	[Fact]
	public void Error_HasExpectedValue()
	{
		// Assert
		((int)PollingStatus.Error).ShouldBe(4);
	}

	[Fact]
	public void Idle_IsDefaultValue()
	{
		// Arrange
		PollingStatus defaultStatus = default;

		// Assert
		defaultStatus.ShouldBe(PollingStatus.Idle);
	}

	[Theory]
	[InlineData(PollingStatus.Idle)]
	[InlineData(PollingStatus.Running)]
	[InlineData(PollingStatus.Paused)]
	[InlineData(PollingStatus.Stopped)]
	[InlineData(PollingStatus.Error)]
	public void BeDefinedForAllValues(PollingStatus status)
	{
		// Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, PollingStatus.Idle)]
	[InlineData(1, PollingStatus.Running)]
	[InlineData(2, PollingStatus.Paused)]
	[InlineData(3, PollingStatus.Stopped)]
	[InlineData(4, PollingStatus.Error)]
	public void CastFromInt_ReturnsCorrectValue(int value, PollingStatus expected)
	{
		// Act
		var status = (PollingStatus)value;

		// Assert
		status.ShouldBe(expected);
	}
}
