// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="ErrorSeverity"/>.
/// </summary>
/// <remarks>
/// Tests the error severity enumeration values and ordering.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Exceptions")]
[Trait("Priority", "0")]
public sealed class ErrorSeverityShould
{
	#region Enum Value Tests

	[Fact]
	public void Information_HasValue0()
	{
		// Assert
		((int)ErrorSeverity.Information).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasValue1()
	{
		// Assert
		((int)ErrorSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void Error_HasValue2()
	{
		// Assert
		((int)ErrorSeverity.Error).ShouldBe(2);
	}

	[Fact]
	public void Critical_HasValue3()
	{
		// Assert
		((int)ErrorSeverity.Critical).ShouldBe(3);
	}

	[Fact]
	public void Fatal_HasValue4()
	{
		// Assert
		((int)ErrorSeverity.Fatal).ShouldBe(4);
	}

	#endregion

	#region Enum Completeness Tests

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<ErrorSeverity>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Theory]
	[InlineData(ErrorSeverity.Information, "Information")]
	[InlineData(ErrorSeverity.Warning, "Warning")]
	[InlineData(ErrorSeverity.Error, "Error")]
	[InlineData(ErrorSeverity.Critical, "Critical")]
	[InlineData(ErrorSeverity.Fatal, "Fatal")]
	public void ToString_ReturnsExpectedName(ErrorSeverity severity, string expectedName)
	{
		// Act
		var result = severity.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Information", ErrorSeverity.Information)]
	[InlineData("Warning", ErrorSeverity.Warning)]
	[InlineData("Error", ErrorSeverity.Error)]
	[InlineData("Critical", ErrorSeverity.Critical)]
	[InlineData("Fatal", ErrorSeverity.Fatal)]
	public void Parse_WithValidString_ReturnsExpectedSeverity(string input, ErrorSeverity expected)
	{
		// Act
		var result = Enum.Parse<ErrorSeverity>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithInvalidString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<ErrorSeverity>("Severe"));
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var result = Enum.TryParse<ErrorSeverity>("Severe", out _);

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
	[InlineData(4, true)]
	[InlineData(5, false)]
	[InlineData(-1, false)]
	public void IsDefined_WithIntValue_ReturnsExpected(int value, bool expected)
	{
		// Act
		var result = Enum.IsDefined(typeof(ErrorSeverity), value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Ordering Tests

	[Fact]
	public void SeverityOrdering_InformationIsLowest()
	{
		// Assert
		((int)ErrorSeverity.Information < (int)ErrorSeverity.Warning).ShouldBeTrue();
		((int)ErrorSeverity.Information < (int)ErrorSeverity.Error).ShouldBeTrue();
		((int)ErrorSeverity.Information < (int)ErrorSeverity.Critical).ShouldBeTrue();
		((int)ErrorSeverity.Information < (int)ErrorSeverity.Fatal).ShouldBeTrue();
	}

	[Fact]
	public void SeverityOrdering_FatalIsHighest()
	{
		// Assert
		((int)ErrorSeverity.Fatal > (int)ErrorSeverity.Information).ShouldBeTrue();
		((int)ErrorSeverity.Fatal > (int)ErrorSeverity.Warning).ShouldBeTrue();
		((int)ErrorSeverity.Fatal > (int)ErrorSeverity.Error).ShouldBeTrue();
		((int)ErrorSeverity.Fatal > (int)ErrorSeverity.Critical).ShouldBeTrue();
	}

	[Fact]
	public void SeverityOrdering_HasCorrectProgression()
	{
		// Assert - Each level is higher than the previous
		((int)ErrorSeverity.Warning > (int)ErrorSeverity.Information).ShouldBeTrue();
		((int)ErrorSeverity.Error > (int)ErrorSeverity.Warning).ShouldBeTrue();
		((int)ErrorSeverity.Critical > (int)ErrorSeverity.Error).ShouldBeTrue();
		((int)ErrorSeverity.Fatal > (int)ErrorSeverity.Critical).ShouldBeTrue();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void CanDetermineAlertThreshold()
	{
		// Arrange
		var warningThreshold = ErrorSeverity.Warning;

		// Act & Assert
		(ErrorSeverity.Information >= warningThreshold).ShouldBeFalse();
		(ErrorSeverity.Warning >= warningThreshold).ShouldBeTrue();
		(ErrorSeverity.Error >= warningThreshold).ShouldBeTrue();
		(ErrorSeverity.Critical >= warningThreshold).ShouldBeTrue();
		(ErrorSeverity.Fatal >= warningThreshold).ShouldBeTrue();
	}

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var severities = Enum.GetValues<ErrorSeverity>();

		// Act & Assert
		foreach (var severity in severities)
		{
			var description = severity switch
			{
				ErrorSeverity.Information => "Informational message",
				ErrorSeverity.Warning => "Warning - investigate",
				ErrorSeverity.Error => "Error - action needed",
				ErrorSeverity.Critical => "Critical - immediate attention",
				ErrorSeverity.Fatal => "Fatal - system failure",
				_ => "Unknown",
			};

			description.ShouldNotBe("Unknown");
		}
	}

	[Theory]
	[InlineData(ErrorSeverity.Information, false)]
	[InlineData(ErrorSeverity.Warning, false)]
	[InlineData(ErrorSeverity.Error, true)]
	[InlineData(ErrorSeverity.Critical, true)]
	[InlineData(ErrorSeverity.Fatal, true)]
	public void CanDetermineIfActionRequired(ErrorSeverity severity, bool actionRequired)
	{
		// Act
		var requiresAction = severity >= ErrorSeverity.Error;

		// Assert
		requiresAction.ShouldBe(actionRequired);
	}

	[Theory]
	[InlineData(ErrorSeverity.Information, false)]
	[InlineData(ErrorSeverity.Warning, false)]
	[InlineData(ErrorSeverity.Error, false)]
	[InlineData(ErrorSeverity.Critical, true)]
	[InlineData(ErrorSeverity.Fatal, true)]
	public void CanDetermineIfPagingRequired(ErrorSeverity severity, bool shouldPage)
	{
		// Act
		var requiresPaging = severity >= ErrorSeverity.Critical;

		// Assert
		requiresPaging.ShouldBe(shouldPage);
	}

	[Fact]
	public void CanSortBySeverity()
	{
		// Arrange
		var errors = new List<ErrorSeverity>
		{
			ErrorSeverity.Warning,
			ErrorSeverity.Fatal,
			ErrorSeverity.Information,
			ErrorSeverity.Critical,
			ErrorSeverity.Error,
		};

		// Act
		var sorted = errors.OrderByDescending(e => e).ToList();

		// Assert
		sorted[0].ShouldBe(ErrorSeverity.Fatal);
		sorted[1].ShouldBe(ErrorSeverity.Critical);
		sorted[2].ShouldBe(ErrorSeverity.Error);
		sorted[3].ShouldBe(ErrorSeverity.Warning);
		sorted[4].ShouldBe(ErrorSeverity.Information);
	}

	#endregion
}
