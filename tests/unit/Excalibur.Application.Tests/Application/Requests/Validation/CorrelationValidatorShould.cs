// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Validation;

namespace Excalibur.Tests.Application.Requests.Validation;

/// <summary>
/// Unit tests for <see cref="CorrelationValidator{TRequest}"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Validation")]
public sealed class CorrelationValidatorShould : UnitTestBase
{
	private readonly CorrelationValidator<TestCorrelatable> _validator = new();

	#region Valid Correlation Tests

	[Fact]
	public void Validate_ValidCorrelationId_ReturnsNoErrors()
	{
		// Arrange
		var correlatable = new TestCorrelatable
		{
			CorrelationId = Guid.NewGuid()
		};

		// Act
		var result = _validator.Validate(correlatable);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.Count.ShouldBe(0);
	}

	#endregion

	#region Invalid Correlation Tests

	[Fact]
	public void Validate_EmptyCorrelationId_ReturnsError()
	{
		// Arrange
		var correlatable = new TestCorrelatable
		{
			CorrelationId = Guid.Empty
		};

		// Act
		var result = _validator.Validate(correlatable);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "CorrelationId");
		result.Errors.ShouldContain(e => e.ErrorMessage.Contains("empty guid"));
	}

	#endregion

	#region Async Validation Tests

	[Fact]
	public async Task ValidateAsync_ValidCorrelationId_ReturnsNoErrors()
	{
		// Arrange
		var correlatable = new TestCorrelatable
		{
			CorrelationId = Guid.NewGuid()
		};

		// Act
		var result = await _validator.ValidateAsync(correlatable);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateAsync_EmptyCorrelationId_ReturnsError()
	{
		// Arrange
		var correlatable = new TestCorrelatable
		{
			CorrelationId = Guid.Empty
		};

		// Act
		var result = await _validator.ValidateAsync(correlatable);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	#endregion

	#region Test Implementation

	private sealed class TestCorrelatable : IAmCorrelatable
	{
		public required Guid CorrelationId { get; init; }
	}

	#endregion
}
