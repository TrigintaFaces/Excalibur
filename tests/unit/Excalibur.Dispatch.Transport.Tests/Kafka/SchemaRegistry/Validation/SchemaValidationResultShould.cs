// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry.Validation;

/// <summary>
/// Unit tests for the <see cref="SchemaValidationResult"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify factory methods and validation result properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaValidationResultShould
{
	#region Success Tests

	[Fact]
	public void Success_ReturnsValidResult()
	{
		// Act
		var result = SchemaValidationResult.Success;

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Success_HasEmptyErrors()
	{
		// Act
		var result = SchemaValidationResult.Success;

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Success_ReturnsSameInstance()
	{
		// Act
		var result1 = SchemaValidationResult.Success;
		var result2 = SchemaValidationResult.Success;

		// Assert
		result1.ShouldBeSameAs(result2);
	}

	#endregion

	#region Failure Tests - Params Array

	[Fact]
	public void Failure_WithSingleError_CreatesInvalidResult()
	{
		// Act
		var result = SchemaValidationResult.Failure("Invalid schema format");

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Failure_WithSingleError_ContainsError()
	{
		// Arrange
		const string error = "Missing required field: id";

		// Act
		var result = SchemaValidationResult.Failure(error);

		// Assert
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe(error);
	}

	[Fact]
	public void Failure_WithMultipleErrors_ContainsAllErrors()
	{
		// Arrange
		var errors = new[] { "Error 1", "Error 2", "Error 3" };

		// Act
		var result = SchemaValidationResult.Failure(errors);

		// Assert
		result.Errors.Count.ShouldBe(3);
		result.Errors.ShouldContain("Error 1");
		result.Errors.ShouldContain("Error 2");
		result.Errors.ShouldContain("Error 3");
	}

	[Fact]
	public void Failure_ParamsArray_ThrowsForNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SchemaValidationResult.Failure((string[])null!));
	}

	#endregion

	#region Failure Tests - IReadOnlyList

	[Fact]
	public void Failure_WithList_CreatesInvalidResult()
	{
		// Arrange
		IReadOnlyList<string> errors = new List<string> { "Error" };

		// Act
		var result = SchemaValidationResult.Failure(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Failure_WithList_ContainsAllErrors()
	{
		// Arrange
		IReadOnlyList<string> errors = new List<string> { "Schema error", "Type mismatch" };

		// Act
		var result = SchemaValidationResult.Failure(errors);

		// Assert
		result.Errors.Count.ShouldBe(2);
		result.Errors.ShouldContain("Schema error");
		result.Errors.ShouldContain("Type mismatch");
	}

	[Fact]
	public void Failure_List_ThrowsForNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SchemaValidationResult.Failure((IReadOnlyList<string>)null!));
	}

	#endregion

	#region Property Tests

	[Fact]
	public void IsValid_IsTrue_ForSuccess()
	{
		// Assert
		SchemaValidationResult.Success.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValid_IsFalse_ForFailure()
	{
		// Act
		var result = SchemaValidationResult.Failure("Any error");

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Errors_IsReadOnly()
	{
		// Act
		var result = SchemaValidationResult.Failure("Error");

		// Assert
		result.Errors.ShouldBeAssignableTo<IReadOnlyList<string>>();
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Properties_AreReadOnly()
	{
		// Assert
		typeof(SchemaValidationResult).GetProperty(nameof(SchemaValidationResult.IsValid)).CanWrite.ShouldBeFalse();
		typeof(SchemaValidationResult).GetProperty(nameof(SchemaValidationResult.Errors)).CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(SchemaValidationResult).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Empty Errors Tests

	[Fact]
	public void Failure_WithEmptyArray_CreatesInvalidResult()
	{
		// Act
		var result = SchemaValidationResult.Failure(Array.Empty<string>());

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
	}

	#endregion
}
