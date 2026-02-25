// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using System.Reflection;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="ErrorConstants"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class ErrorConstantsShould
{
	[Fact]
	public void AllConstants_Should_BeNonEmpty()
	{
		// Arrange
		var fields = typeof(ErrorConstants)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

		// Act & Assert
		foreach (var field in fields)
		{
			var value = (string?)field.GetValue(null);
			value.ShouldNotBeNullOrEmpty($"Field {field.Name} should not be null or empty");
		}
	}

	[Fact]
	public void Should_ContainExpectedConstants()
	{
		// Assert - spot-check key constants
		ErrorConstants.ConnectionStringRequired.ShouldBe("Connection string required.");
		ErrorConstants.MessageCannotBeNull.ShouldBe("Message cannot be null.");
		ErrorConstants.ValidationFailed.ShouldBe("Validation failed.");
		ErrorConstants.TimeoutMustBePositive.ShouldBe("Timeout must be positive.");
	}

	[Fact]
	public void Should_HaveSubstantialNumberOfConstants()
	{
		// Arrange
		var count = typeof(ErrorConstants)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Count(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

		// Assert - should have a significant number of error constants
		count.ShouldBeGreaterThan(50);
	}
}
