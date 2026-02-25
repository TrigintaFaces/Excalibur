// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineComplexity"/>.
/// </summary>
/// <remarks>
/// Tests the pipeline complexity enumeration values.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class PipelineComplexityShould
{
	#region Enum Value Tests

	[Fact]
	public void Standard_HasValue0()
	{
		// Assert
		((int)PipelineComplexity.Standard).ShouldBe(0);
	}

	[Fact]
	public void Reduced_HasValue1()
	{
		// Assert
		((int)PipelineComplexity.Reduced).ShouldBe(1);
	}

	[Fact]
	public void Minimal_HasValue2()
	{
		// Assert
		((int)PipelineComplexity.Minimal).ShouldBe(2);
	}

	[Fact]
	public void Direct_HasValue3()
	{
		// Assert
		((int)PipelineComplexity.Direct).ShouldBe(3);
	}

	#endregion

	#region Enum Completeness Tests

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<PipelineComplexity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(PipelineComplexity.Standard, "Standard")]
	[InlineData(PipelineComplexity.Reduced, "Reduced")]
	[InlineData(PipelineComplexity.Minimal, "Minimal")]
	[InlineData(PipelineComplexity.Direct, "Direct")]
	public void ToString_ReturnsExpectedName(PipelineComplexity level, string expectedName)
	{
		// Act
		var result = level.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Standard", PipelineComplexity.Standard)]
	[InlineData("Reduced", PipelineComplexity.Reduced)]
	[InlineData("Minimal", PipelineComplexity.Minimal)]
	[InlineData("Direct", PipelineComplexity.Direct)]
	public void Parse_WithValidString_ReturnsExpectedLevel(string input, PipelineComplexity expected)
	{
		// Act
		var result = Enum.Parse<PipelineComplexity>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithInvalidString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<PipelineComplexity>("SuperFast"));
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var result = Enum.TryParse<PipelineComplexity>("SuperFast", out _);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsDefined Tests

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, true)]
	[InlineData(4, false)]
	[InlineData(-1, false)]
	public void IsDefined_WithIntValue_ReturnsExpected(int value, bool expected)
	{
		// Act
		var result = Enum.IsDefined(typeof(PipelineComplexity), value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Ordering Tests

	[Fact]
	public void PipelineComplexity_StandardIsLowest()
	{
		// Assert
		((int)PipelineComplexity.Standard < (int)PipelineComplexity.Reduced).ShouldBeTrue();
		((int)PipelineComplexity.Standard < (int)PipelineComplexity.Minimal).ShouldBeTrue();
		((int)PipelineComplexity.Standard < (int)PipelineComplexity.Direct).ShouldBeTrue();
	}

	[Fact]
	public void PipelineComplexity_DirectIsHighest()
	{
		// Assert
		((int)PipelineComplexity.Direct > (int)PipelineComplexity.Standard).ShouldBeTrue();
		((int)PipelineComplexity.Direct > (int)PipelineComplexity.Reduced).ShouldBeTrue();
		((int)PipelineComplexity.Direct > (int)PipelineComplexity.Minimal).ShouldBeTrue();
	}

	[Fact]
	public void PipelineComplexity_HasCorrectProgression()
	{
		// Assert - Each level is higher than the previous
		((int)PipelineComplexity.Reduced > (int)PipelineComplexity.Standard).ShouldBeTrue();
		((int)PipelineComplexity.Minimal > (int)PipelineComplexity.Reduced).ShouldBeTrue();
		((int)PipelineComplexity.Direct > (int)PipelineComplexity.Minimal).ShouldBeTrue();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void CanDetermineMiddlewareCount()
	{
		// Arrange & Act
		var standardCount = GetMiddlewareCount(PipelineComplexity.Standard);
		var reducedCount = GetMiddlewareCount(PipelineComplexity.Reduced);
		var minimalCount = GetMiddlewareCount(PipelineComplexity.Minimal);
		var directCount = GetMiddlewareCount(PipelineComplexity.Direct);

		// Assert - Higher level = fewer middleware
		standardCount.ShouldBeGreaterThan(reducedCount);
		reducedCount.ShouldBeGreaterThan(minimalCount);
		minimalCount.ShouldBeGreaterThan(directCount);
		directCount.ShouldBe(0);
	}

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var levels = Enum.GetValues<PipelineComplexity>();

		// Act & Assert
		foreach (var level in levels)
		{
			var description = level switch
			{
				PipelineComplexity.Standard => "All middleware enabled",
				PipelineComplexity.Reduced => "Non-essential middleware removed",
				PipelineComplexity.Minimal => "Only essential middleware",
				PipelineComplexity.Direct => "Zero middleware overhead",
				_ => "Unknown",
			};

			description.ShouldNotBe("Unknown");
		}
	}

	[Theory]
	[InlineData(PipelineComplexity.Standard, true)]
	[InlineData(PipelineComplexity.Reduced, true)]
	[InlineData(PipelineComplexity.Minimal, false)]
	[InlineData(PipelineComplexity.Direct, false)]
	public void CanDetermineIfLoggingEnabled(PipelineComplexity level, bool loggingExpected)
	{
		// Act
		var isLoggingEnabled = IsLoggingEnabled(level);

		// Assert
		isLoggingEnabled.ShouldBe(loggingExpected);
	}

	private static int GetMiddlewareCount(PipelineComplexity level)
	{
		return level switch
		{
			PipelineComplexity.Standard => 10,
			PipelineComplexity.Reduced => 5,
			PipelineComplexity.Minimal => 2,
			PipelineComplexity.Direct => 0,
			_ => 10,
		};
	}

	private static bool IsLoggingEnabled(PipelineComplexity level)
	{
		return level < PipelineComplexity.Minimal;
	}

	#endregion
}
