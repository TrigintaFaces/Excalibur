// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SerializableValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializableValidationResultShould
{
	[Fact]
	public void Success_ReturnsValidResult()
	{
		// Act
		var result = SerializableValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Failed_ReturnsInvalidResult()
	{
		// Act
		var result = SerializableValidationResult.Failed("error1", "error2");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void Failed_WithNoErrors_ReturnsInvalidResult()
	{
		// Act
		var result = SerializableValidationResult.Failed();

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Errors_SetToNull_DefaultsToEmptyCollection()
	{
		// Arrange
		var result = new SerializableValidationResult();

		// Act
		result.Errors = null!;

		// Assert
		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void IsValid_CanBeSetDirectly()
	{
		// Arrange
		var result = new SerializableValidationResult();

		// Act
		result.IsValid = false;

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Errors_CanBeSetDirectly()
	{
		// Arrange
		var result = new SerializableValidationResult();
		var errors = new object[] { "err1", "err2", "err3" };

		// Act
		result.Errors = errors;

		// Assert
		result.Errors.Count.ShouldBe(3);
	}

	[Fact]
	public void Failed_ReturnsSerializableValidationResult()
	{
		// Act
		SerializableValidationResult result = SerializableValidationResult.Failed("some error");

		// Assert
		result.ShouldBeOfType<SerializableValidationResult>();
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Success_ImplementsIValidationResult()
	{
		// Act
		IValidationResult result = SerializableValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Failed_ImplementsIValidationResult()
	{
		// Act
		IValidationResult result = SerializableValidationResult.Failed("err");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void DefaultConstructor_IsNotValid()
	{
		// Act
		var result = new SerializableValidationResult();

		// Assert - default bool is false
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void DefaultConstructor_HasEmptyErrors()
	{
		// Act
		var result = new SerializableValidationResult();

		// Assert
		result.Errors.ShouldBeEmpty();
	}
}
