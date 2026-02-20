// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="ValidationIssue"/>.
/// </summary>
/// <remarks>
/// Tests the validation issue record type.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class ValidationIssueShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange & Act
		var issue = new ValidationIssue(ValidationSeverity.Error, "Test error message");

		// Assert
		_ = issue.ShouldNotBeNull();
		issue.Severity.ShouldBe(ValidationSeverity.Error);
		issue.Message.ShouldBe("Test error message");
	}

	[Theory]
	[InlineData(ValidationSeverity.Info)]
	[InlineData(ValidationSeverity.Warning)]
	[InlineData(ValidationSeverity.Error)]
	public void Constructor_WithVariousSeverities_Works(ValidationSeverity severity)
	{
		// Arrange & Act
		var issue = new ValidationIssue(severity, "Test message");

		// Assert
		issue.Severity.ShouldBe(severity);
	}

	[Fact]
	public void Constructor_WithEmptyMessage_Works()
	{
		// Arrange & Act
		var issue = new ValidationIssue(ValidationSeverity.Warning, string.Empty);

		// Assert
		issue.Message.ShouldBe(string.Empty);
	}

	#endregion

	#region Severity Property Tests

	[Fact]
	public void Severity_ReturnsCorrectValue()
	{
		// Arrange
		var issue = new ValidationIssue(ValidationSeverity.Warning, "Warning message");

		// Act & Assert
		issue.Severity.ShouldBe(ValidationSeverity.Warning);
	}

	#endregion

	#region Message Property Tests

	[Fact]
	public void Message_ReturnsCorrectValue()
	{
		// Arrange
		var issue = new ValidationIssue(ValidationSeverity.Error, "Configuration error detected");

		// Act & Assert
		issue.Message.ShouldBe("Configuration error detected");
	}

	[Fact]
	public void Message_WithLongMessage_Works()
	{
		// Arrange
		var longMessage = new string('x', 5000);
		var issue = new ValidationIssue(ValidationSeverity.Info, longMessage);

		// Act & Assert
		issue.Message.ShouldBe(longMessage);
		issue.Message.Length.ShouldBe(5000);
	}

	[Fact]
	public void Message_WithSpecialCharacters_Works()
	{
		// Arrange
		var message = "Error: Missing required field 'Name' (value was null)";
		var issue = new ValidationIssue(ValidationSeverity.Error, message);

		// Act & Assert
		issue.Message.ShouldBe(message);
	}

	#endregion

	#region Record Equality Tests

	[Fact]
	public void Equals_SameValues_ReturnsTrue()
	{
		// Arrange
		var issue1 = new ValidationIssue(ValidationSeverity.Error, "Error message");
		var issue2 = new ValidationIssue(ValidationSeverity.Error, "Error message");

		// Act & Assert
		issue1.ShouldBe(issue2);
	}

	[Fact]
	public void Equals_DifferentSeverity_ReturnsFalse()
	{
		// Arrange
		var issue1 = new ValidationIssue(ValidationSeverity.Error, "Message");
		var issue2 = new ValidationIssue(ValidationSeverity.Warning, "Message");

		// Act & Assert
		issue1.ShouldNotBe(issue2);
	}

	[Fact]
	public void Equals_DifferentMessage_ReturnsFalse()
	{
		// Arrange
		var issue1 = new ValidationIssue(ValidationSeverity.Error, "Message 1");
		var issue2 = new ValidationIssue(ValidationSeverity.Error, "Message 2");

		// Act & Assert
		issue1.ShouldNotBe(issue2);
	}

	[Fact]
	public void GetHashCode_SameValues_ReturnsSameHashCode()
	{
		// Arrange
		var issue1 = new ValidationIssue(ValidationSeverity.Warning, "Test");
		var issue2 = new ValidationIssue(ValidationSeverity.Warning, "Test");

		// Act & Assert
		issue1.GetHashCode().ShouldBe(issue2.GetHashCode());
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsExpectedFormat()
	{
		// Arrange
		var issue = new ValidationIssue(ValidationSeverity.Error, "Test message");

		// Act
		var result = issue.ToString();

		// Assert
		result.ShouldContain("ValidationIssue");
		result.ShouldContain("Error");
		result.ShouldContain("Test message");
	}

	#endregion

	#region Deconstruction Tests

	[Fact]
	public void Deconstruct_ExtractsValues()
	{
		// Arrange
		var issue = new ValidationIssue(ValidationSeverity.Warning, "Warning message");

		// Act
		var (severity, message) = issue;

		// Assert
		severity.ShouldBe(ValidationSeverity.Warning);
		message.ShouldBe("Warning message");
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CanChangeSeverity()
	{
		// Arrange
		var original = new ValidationIssue(ValidationSeverity.Warning, "Message");

		// Act
		var modified = original with { Severity = ValidationSeverity.Error };

		// Assert
		modified.Severity.ShouldBe(ValidationSeverity.Error);
		modified.Message.ShouldBe("Message");
	}

	[Fact]
	public void With_CanChangeMessage()
	{
		// Arrange
		var original = new ValidationIssue(ValidationSeverity.Error, "Original");

		// Act
		var modified = original with { Message = "Modified" };

		// Assert
		modified.Severity.ShouldBe(ValidationSeverity.Error);
		modified.Message.ShouldBe("Modified");
	}

	#endregion
}
