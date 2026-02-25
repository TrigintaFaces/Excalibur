// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="PatternHealthStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class PatternHealthStatusShould
{
	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<PatternHealthStatus>();

		// Assert
		values.Length.ShouldBe(5);
		values.ShouldContain(PatternHealthStatus.Unknown);
		values.ShouldContain(PatternHealthStatus.Healthy);
		values.ShouldContain(PatternHealthStatus.Degraded);
		values.ShouldContain(PatternHealthStatus.Unhealthy);
		values.ShouldContain(PatternHealthStatus.Critical);
	}

	[Fact]
	public void Unknown_HasExpectedValue()
	{
		// Assert
		((int)PatternHealthStatus.Unknown).ShouldBe(0);
	}

	[Fact]
	public void Healthy_HasExpectedValue()
	{
		// Assert
		((int)PatternHealthStatus.Healthy).ShouldBe(1);
	}

	[Fact]
	public void Degraded_HasExpectedValue()
	{
		// Assert
		((int)PatternHealthStatus.Degraded).ShouldBe(2);
	}

	[Fact]
	public void Unhealthy_HasExpectedValue()
	{
		// Assert
		((int)PatternHealthStatus.Unhealthy).ShouldBe(3);
	}

	[Fact]
	public void Critical_HasExpectedValue()
	{
		// Assert
		((int)PatternHealthStatus.Critical).ShouldBe(4);
	}

	[Fact]
	public void Unknown_IsDefaultValue()
	{
		// Arrange
		PatternHealthStatus defaultStatus = default;

		// Assert
		defaultStatus.ShouldBe(PatternHealthStatus.Unknown);
	}

	[Theory]
	[InlineData(PatternHealthStatus.Unknown)]
	[InlineData(PatternHealthStatus.Healthy)]
	[InlineData(PatternHealthStatus.Degraded)]
	[InlineData(PatternHealthStatus.Unhealthy)]
	[InlineData(PatternHealthStatus.Critical)]
	public void BeDefinedForAllValues(PatternHealthStatus status)
	{
		// Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, PatternHealthStatus.Unknown)]
	[InlineData(1, PatternHealthStatus.Healthy)]
	[InlineData(2, PatternHealthStatus.Degraded)]
	[InlineData(3, PatternHealthStatus.Unhealthy)]
	[InlineData(4, PatternHealthStatus.Critical)]
	public void CastFromInt_ReturnsCorrectValue(int value, PatternHealthStatus expected)
	{
		// Act
		var status = (PatternHealthStatus)value;

		// Assert
		status.ShouldBe(expected);
	}

	[Fact]
	public void HaveHealthStatusesOrderedBySeverity()
	{
		// Assert - Values should be ordered from most healthy to most critical
		(PatternHealthStatus.Unknown < PatternHealthStatus.Healthy).ShouldBeTrue();
		(PatternHealthStatus.Healthy < PatternHealthStatus.Degraded).ShouldBeTrue();
		(PatternHealthStatus.Degraded < PatternHealthStatus.Unhealthy).ShouldBeTrue();
		(PatternHealthStatus.Unhealthy < PatternHealthStatus.Critical).ShouldBeTrue();
	}
}
