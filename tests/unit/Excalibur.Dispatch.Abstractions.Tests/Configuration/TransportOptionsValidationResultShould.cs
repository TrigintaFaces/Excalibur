// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Abstractions.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="TransportOptionsValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportOptionsValidationResultShould
{
	[Fact]
	public void Success_ReturnsValidResult()
	{
		// Act
		var result = TransportOptionsValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Success_ReturnsSameInstance()
	{
		// Act
		var result1 = TransportOptionsValidationResult.Success();
		var result2 = TransportOptionsValidationResult.Success();

		// Assert
		ReferenceEquals(result1, result2).ShouldBeTrue();
	}

	[Fact]
	public void Failed_WithParamsArray_ReturnsInvalidResult()
	{
		// Act
		var result = TransportOptionsValidationResult.Failed("Error 1", "Error 2");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].ShouldBe("Error 1");
		result.Errors[1].ShouldBe("Error 2");
	}

	[Fact]
	public void Failed_WithEnumerable_ReturnsInvalidResult()
	{
		// Arrange
		var errors = new List<string> { "Connection string required", "Port out of range" };

		// Act
		var result = TransportOptionsValidationResult.Failed(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].ShouldBe("Connection string required");
		result.Errors[1].ShouldBe("Port out of range");
	}

	[Fact]
	public void Failed_WithSingleError_ReturnsInvalidResult()
	{
		// Act
		var result = TransportOptionsValidationResult.Failed("Single error");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe("Single error");
	}

	[Fact]
	public void Failed_WithEmptyErrors_ReturnsInvalidResult()
	{
		// Act
		var result = TransportOptionsValidationResult.Failed(Array.Empty<string>());

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
	}
}
