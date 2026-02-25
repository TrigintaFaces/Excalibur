// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="ProblemDetailsTypes"/> constants class.
/// Validates URN format consistency and RFC 9457 compliance.
/// </summary>
/// <remarks>
/// Sprint 444 S444.5: Unit tests for ProblemDetailsTypes format validation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ProblemDetails")]
public sealed class ProblemDetailsTypesShould : UnitTestBase
{
	#region Prefix Tests

	[Fact]
	public void HaveCorrectPrefixValue()
	{
		// Arrange & Act & Assert
		ProblemDetailsTypes.Prefix.ShouldBe("urn:dispatch:error:");
	}

	[Fact]
	public void HavePrefixStartingWithUrn()
	{
		// Arrange & Act & Assert
		ProblemDetailsTypes.Prefix.ShouldStartWith("urn:");
	}

	#endregion

	#region URN Format Tests

	[Theory]
	[InlineData(nameof(ProblemDetailsTypes.Validation), "urn:dispatch:error:validation")]
	[InlineData(nameof(ProblemDetailsTypes.NotFound), "urn:dispatch:error:not-found")]
	[InlineData(nameof(ProblemDetailsTypes.Conflict), "urn:dispatch:error:conflict")]
	[InlineData(nameof(ProblemDetailsTypes.Forbidden), "urn:dispatch:error:forbidden")]
	[InlineData(nameof(ProblemDetailsTypes.Unauthorized), "urn:dispatch:error:unauthorized")]
	[InlineData(nameof(ProblemDetailsTypes.Timeout), "urn:dispatch:error:timeout")]
	[InlineData(nameof(ProblemDetailsTypes.RateLimited), "urn:dispatch:error:rate-limited")]
	[InlineData(nameof(ProblemDetailsTypes.Internal), "urn:dispatch:error:internal")]
	[InlineData(nameof(ProblemDetailsTypes.Routing), "urn:dispatch:error:routing")]
	[InlineData(nameof(ProblemDetailsTypes.Transport), "urn:dispatch:error:transport")]
	[InlineData(nameof(ProblemDetailsTypes.Serialization), "urn:dispatch:error:serialization")]
	[InlineData(nameof(ProblemDetailsTypes.Concurrency), "urn:dispatch:error:concurrency")]
	[InlineData(nameof(ProblemDetailsTypes.HandlerNotFound), "urn:dispatch:error:handler-not-found")]
	[InlineData(nameof(ProblemDetailsTypes.HandlerError), "urn:dispatch:error:handler-error")]
	[InlineData(nameof(ProblemDetailsTypes.MappingFailed), "urn:dispatch:error:mapping-failed")]
	[InlineData(nameof(ProblemDetailsTypes.BackgroundExecution), "urn:dispatch:error:background-execution")]
	public void HaveCorrectUrnValue(string constantName, string expectedValue)
	{
		// Arrange
		var field = typeof(ProblemDetailsTypes).GetField(constantName, BindingFlags.Public | BindingFlags.Static);

		// Act
		var actualValue = field?.GetValue(null) as string;

		// Assert
		actualValue.ShouldBe(expectedValue);
	}

	[Fact]
	public void HaveAllConstantsStartingWithPrefix()
	{
		// Arrange
		var constants = GetAllTypeConstants();

		// Act & Assert
		foreach (var (name, value) in constants)
		{
			value.ShouldStartWith(ProblemDetailsTypes.Prefix, customMessage: $"Constant '{name}' should start with prefix");
		}
	}

	[Fact]
	public void HaveAllConstantsUsingLowercaseKebabCase()
	{
		// Arrange
		var constants = GetAllTypeConstants();

		// Act & Assert
		foreach (var (name, value) in constants)
		{
			// Extract the suffix after the prefix
			var suffix = value[ProblemDetailsTypes.Prefix.Length..];

			// Verify no uppercase letters (all chars should be lowercase or hyphen)
			var hasNoUppercase = suffix.All(c => !char.IsUpper(c));
			hasNoUppercase.ShouldBeTrue($"Constant '{name}' suffix should be lowercase (no uppercase letters)");

			// Verify no underscores (kebab-case uses hyphens)
			suffix.ShouldNotContain("_");
		}
	}

	[Fact]
	public void HaveNoEmptySuffixes()
	{
		// Arrange
		var constants = GetAllTypeConstants();

		// Act & Assert
		foreach (var (name, value) in constants)
		{
			var suffix = value[ProblemDetailsTypes.Prefix.Length..];
			suffix.ShouldNotBeNullOrWhiteSpace($"Constant '{name}' should have a non-empty suffix");
		}
	}

	#endregion

	#region No Duplicates Tests

	[Fact]
	public void HaveNoDuplicateValues()
	{
		// Arrange
		var constants = GetAllTypeConstants();
		var values = constants.Select(c => c.Value).ToList();

		// Act
		var duplicates = values
			.GroupBy(v => v)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty($"Found duplicate values: {string.Join(", ", duplicates)}");
	}

	#endregion

	#region No Legacy URLs Tests

	[Fact]
	public void NotContainLegacyHttpsUrls()
	{
		// Arrange
		var constants = GetAllTypeConstants();

		// Act & Assert
		foreach (var (name, value) in constants)
		{
			value.ShouldNotContain("https://");
			value.ShouldNotContain("http://");
		}
	}

	[Fact]
	public void NotContainErrorsDispatchComUrl()
	{
		// Arrange
		var constants = GetAllTypeConstants();

		// Act & Assert
		foreach (var (name, value) in constants)
		{
			value.ShouldNotContain("errors.dispatch.com");
		}
	}

	#endregion

	#region Compile-Time Const Tests

	[Fact]
	public void BeCompileTimeConstants()
	{
		// Arrange
		var fields = typeof(ProblemDetailsTypes)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.FieldType == typeof(string) && f.IsLiteral)
			.ToList();

		// Act & Assert
		fields.Count.ShouldBeGreaterThan(15, "Should have at least 16 const string fields (including Prefix)");

		foreach (var field in fields)
		{
			field.IsLiteral.ShouldBeTrue($"Field '{field.Name}' should be a const (compile-time literal)");
		}
	}

	#endregion

	#region Static Class Tests

	[Fact]
	public void BeStaticClass()
	{
		// Arrange
		var type = typeof(ProblemDetailsTypes);

		// Act & Assert
		type.IsAbstract.ShouldBeTrue("Class should be abstract (part of static class)");
		type.IsSealed.ShouldBeTrue("Class should be sealed (part of static class)");
	}

	[Fact]
	public void BeInDispatchAbstractionsNamespace()
	{
		// Arrange
		var type = typeof(ProblemDetailsTypes);

		// Act & Assert
		type.Namespace.ShouldBe("Excalibur.Dispatch.Abstractions");
	}

	#endregion

	#region Expected Constants Count

	[Fact]
	public void HaveExpectedNumberOfErrorTypes()
	{
		// Arrange
		var constants = GetAllTypeConstants();

		// Act & Assert
		// 16 error types (excluding Prefix)
		constants.Count.ShouldBe(16, "Should have exactly 16 error type constants");
	}

	#endregion

	#region Helper Methods

	private static List<(string Name, string Value)> GetAllTypeConstants()
	{
		return typeof(ProblemDetailsTypes)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.FieldType == typeof(string) && f.IsLiteral && f.Name != "Prefix")
			.Select(f => (f.Name, Value: (string)f.GetValue(null)!))
			.ToList();
	}

	#endregion
}
