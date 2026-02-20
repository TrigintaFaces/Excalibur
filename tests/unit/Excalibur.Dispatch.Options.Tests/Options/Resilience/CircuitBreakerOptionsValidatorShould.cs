// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOptionsValidator"/>.
/// Sprint 561 S561.53: IValidateOptions implementation tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CircuitBreakerOptionsValidatorShould
{
	private readonly CircuitBreakerOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void FailWhenFailureThresholdIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.FailureThreshold));
	}

	[Fact]
	public void FailWhenFailureThresholdIsNegative()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = -1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.FailureThreshold));
	}

	[Fact]
	public void FailWhenSuccessThresholdIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { SuccessThreshold = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.SuccessThreshold));
	}

	[Fact]
	public void FailWhenOpenDurationIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { OpenDuration = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OpenDuration));
	}

	[Fact]
	public void FailWhenOpenDurationIsNegative()
	{
		// Arrange
		var options = new CircuitBreakerOptions { OpenDuration = TimeSpan.FromSeconds(-1) };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OpenDuration));
	}

	[Fact]
	public void FailWhenOperationTimeoutIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { OperationTimeout = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
	}

	[Fact]
	public void FailWhenMaxHalfOpenTestsIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { MaxHalfOpenTests = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.MaxHalfOpenTests));
	}

	[Fact]
	public void FailWhenOperationTimeoutExceedsOpenDuration()
	{
		// Arrange - OperationTimeout >= OpenDuration should fail
		var options = new CircuitBreakerOptions
		{
			OperationTimeout = TimeSpan.FromSeconds(60),
			OpenDuration = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OpenDuration));
	}

	[Fact]
	public void FailWhenOperationTimeoutEqualsOpenDuration()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			OperationTimeout = TimeSpan.FromSeconds(30),
			OpenDuration = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OpenDuration));
	}

	[Fact]
	public void FailWhenSuccessThresholdExceedsMaxHalfOpenTests()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			SuccessThreshold = 10,
			MaxHalfOpenTests = 3,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.SuccessThreshold));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.MaxHalfOpenTests));
	}

	[Fact]
	public void SucceedWhenSuccessThresholdEqualsMaxHalfOpenTests()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			SuccessThreshold = 3,
			MaxHalfOpenTests = 3,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 0,
			SuccessThreshold = 0,
			OpenDuration = TimeSpan.Zero,
			OperationTimeout = TimeSpan.Zero,
			MaxHalfOpenTests = 0,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.FailureThreshold));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.SuccessThreshold));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OpenDuration));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.MaxHalfOpenTests));
	}
}
