// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="ValidationSeverity"/>.
/// </summary>
/// <remarks>
/// Tests the validation severity enumeration values.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class ValidationSeverityShould
{
	#region Enum Value Tests

	[Fact]
	public void Info_HasValue0()
	{
		// Assert
		((int)ValidationSeverity.Info).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasValue1()
	{
		// Assert
		((int)ValidationSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void Error_HasValue2()
	{
		// Assert
		((int)ValidationSeverity.Error).ShouldBe(2);
	}

	#endregion

	#region Enum Completeness Tests

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<ValidationSeverity>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Theory]
	[InlineData(ValidationSeverity.Info, "Info")]
	[InlineData(ValidationSeverity.Warning, "Warning")]
	[InlineData(ValidationSeverity.Error, "Error")]
	public void ToString_ReturnsExpectedName(ValidationSeverity severity, string expectedName)
	{
		// Act
		var result = severity.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Info", ValidationSeverity.Info)]
	[InlineData("Warning", ValidationSeverity.Warning)]
	[InlineData("Error", ValidationSeverity.Error)]
	public void Parse_WithValidString_ReturnsExpectedSeverity(string input, ValidationSeverity expected)
	{
		// Act
		var result = Enum.Parse<ValidationSeverity>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithInvalidString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<ValidationSeverity>("Critical"));
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var result = Enum.TryParse<ValidationSeverity>("Critical", out _);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsDefined Tests

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, false)]
	[InlineData(-1, false)]
	public void IsDefined_WithIntValue_ReturnsExpected(int value, bool expected)
	{
		// Act
		var result = Enum.IsDefined(typeof(ValidationSeverity), value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Ordering Tests

	[Fact]
	public void SeverityOrdering_InfoIsLowest()
	{
		// Assert
		((int)ValidationSeverity.Info < (int)ValidationSeverity.Warning).ShouldBeTrue();
		((int)ValidationSeverity.Info < (int)ValidationSeverity.Error).ShouldBeTrue();
	}

	[Fact]
	public void SeverityOrdering_WarningIsMiddle()
	{
		// Assert
		((int)ValidationSeverity.Warning > (int)ValidationSeverity.Info).ShouldBeTrue();
		((int)ValidationSeverity.Warning < (int)ValidationSeverity.Error).ShouldBeTrue();
	}

	[Fact]
	public void SeverityOrdering_ErrorIsHighest()
	{
		// Assert
		((int)ValidationSeverity.Error > (int)ValidationSeverity.Info).ShouldBeTrue();
		((int)ValidationSeverity.Error > (int)ValidationSeverity.Warning).ShouldBeTrue();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void CanFilterBySeverity()
	{
		// Arrange
		var issues = new[]
		{
			(Severity: ValidationSeverity.Info, Message: "Info 1"),
			(Severity: ValidationSeverity.Warning, Message: "Warning 1"),
			(Severity: ValidationSeverity.Error, Message: "Error 1"),
			(Severity: ValidationSeverity.Info, Message: "Info 2"),
			(Severity: ValidationSeverity.Error, Message: "Error 2"),
		};

		// Act
		var errorsOnly = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
		var warningsAndAbove = issues.Where(i => i.Severity >= ValidationSeverity.Warning).ToList();

		// Assert
		errorsOnly.Count.ShouldBe(2);
		warningsAndAbove.Count.ShouldBe(3);
	}

	[Fact]
	public void CanDetermineIfSynthesisShouldProceed()
	{
		// Arrange
		var hasErrors = ValidationSeverity.Error;
		var hasWarningsOnly = ValidationSeverity.Warning;
		var hasInfoOnly = ValidationSeverity.Info;

		// Act & Assert - Error prevents synthesis
		ShouldProceedWithSynthesis(hasErrors).ShouldBeFalse();

		// Act & Assert - Warning allows synthesis
		ShouldProceedWithSynthesis(hasWarningsOnly).ShouldBeTrue();

		// Act & Assert - Info allows synthesis
		ShouldProceedWithSynthesis(hasInfoOnly).ShouldBeTrue();
	}

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var severities = Enum.GetValues<ValidationSeverity>();

		// Act & Assert
		foreach (var severity in severities)
		{
			var action = severity switch
			{
				ValidationSeverity.Info => "Log",
				ValidationSeverity.Warning => "Warn",
				ValidationSeverity.Error => "Fail",
				_ => "Unknown",
			};

			action.ShouldNotBe("Unknown");
		}
	}

	private static bool ShouldProceedWithSynthesis(ValidationSeverity maxSeverity)
	{
		return maxSeverity < ValidationSeverity.Error;
	}

	#endregion
}
