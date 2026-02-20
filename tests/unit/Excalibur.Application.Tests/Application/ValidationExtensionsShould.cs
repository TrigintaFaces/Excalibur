// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application;

using FluentValidation;

namespace Excalibur.Tests.Application;

/// <summary>
/// Unit tests for <see cref="ValidationExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Validation")]
public sealed class ValidationExtensionsShould : UnitTestBase
{
	#region IsValidTenantId Tests

	[Fact]
	public void IsValidTenantId_ValidTenantId_ReturnsNoErrors()
	{
		// Arrange
		var validator = new TenantIdValidator();
		var data = new TestDataWithTenantId { TenantId = "tenant-123" };

		// Act
		var result = validator.Validate(data);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValidTenantId_EmptyTenantId_ReturnsError()
	{
		// Arrange
		var validator = new TenantIdValidator();
		var data = new TestDataWithTenantId { TenantId = string.Empty };

		// Act
		var result = validator.Validate(data);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "TenantId");
	}

	[Fact]
	public void IsValidTenantId_NullTenantId_ReturnsError()
	{
		// Arrange
		var validator = new TenantIdValidator();
		var data = new TestDataWithTenantId { TenantId = null! };

		// Act
		var result = validator.Validate(data);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void IsValidTenantId_WhitespaceTenantId_ReturnsError()
	{
		// Arrange
		var validator = new TenantIdValidator();
		var data = new TestDataWithTenantId { TenantId = "   " };

		// Act
		var result = validator.Validate(data);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	#endregion

	#region IsValidCorrelationId Tests

	[Fact]
	public void IsValidCorrelationId_ValidGuid_ReturnsNoErrors()
	{
		// Arrange
		var validator = new CorrelationIdValidator();
		var data = new TestDataWithCorrelationId { CorrelationId = Guid.NewGuid() };

		// Act
		var result = validator.Validate(data);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValidCorrelationId_EmptyGuid_ReturnsError()
	{
		// Arrange
		var validator = new CorrelationIdValidator();
		var data = new TestDataWithCorrelationId { CorrelationId = Guid.Empty };

		// Act
		var result = validator.Validate(data);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "CorrelationId");
		result.Errors.ShouldContain(e => e.ErrorMessage.Contains("empty guid"));
	}

	#endregion

	#region Test Implementations

	private sealed class TestDataWithTenantId
	{
		public required string TenantId { get; init; }
	}

	private sealed class TenantIdValidator : AbstractValidator<TestDataWithTenantId>
	{
		public TenantIdValidator()
		{
			_ = RuleFor(x => x.TenantId).IsValidTenantId();
		}
	}

	private sealed class TestDataWithCorrelationId
	{
		public Guid CorrelationId { get; init; }
	}

	private sealed class CorrelationIdValidator : AbstractValidator<TestDataWithCorrelationId>
	{
		public CorrelationIdValidator()
		{
			_ = RuleFor(x => x.CorrelationId).IsValidCorrelationId();
		}
	}

	#endregion
}
