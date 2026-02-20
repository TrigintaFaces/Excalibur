// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Unit tests for <see cref="SagaOptionsValidator"/>.
/// Sprint 561 S561.53: IValidateOptions implementation tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SagaOptionsValidatorShould
{
	private readonly SagaOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new SagaOptions();

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
	public void FailWhenMaxRetryAttemptsIsNegative()
	{
		// Arrange
		var options = new SagaOptions { MaxRetryAttempts = -1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.MaxRetryAttempts));
	}

	[Fact]
	public void FailWhenMaxConcurrencyIsZero()
	{
		// Arrange
		var options = new SagaOptions { MaxConcurrency = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.MaxConcurrency));
	}

	[Fact]
	public void FailWhenDefaultTimeoutIsZero()
	{
		// Arrange
		var options = new SagaOptions { DefaultTimeout = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.DefaultTimeout));
	}

	[Fact]
	public void FailWhenRetryDelayIsNegative()
	{
		// Arrange
		var options = new SagaOptions { RetryDelay = TimeSpan.FromMinutes(-1) };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.RetryDelay));
	}

	[Fact]
	public void FailWhenRetryDelayExceedsDefaultTimeout()
	{
		// Arrange
		var options = new SagaOptions
		{
			MaxRetryAttempts = 3,
			RetryDelay = TimeSpan.FromMinutes(60),
			DefaultTimeout = TimeSpan.FromMinutes(30),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.RetryDelay));
		result.FailureMessage.ShouldContain(nameof(SagaOptions.DefaultTimeout));
	}

	[Fact]
	public void SucceedWhenRetryDelayEqualsTimeout_ButRetriesDisabled()
	{
		// Arrange - when MaxRetryAttempts is 0, cross-property check is skipped
		var options = new SagaOptions
		{
			MaxRetryAttempts = 0,
			RetryDelay = TimeSpan.FromMinutes(60),
			DefaultTimeout = TimeSpan.FromMinutes(30),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenSagaRetentionPeriodIsZero()
	{
		// Arrange
		var options = new SagaOptions { SagaRetentionPeriod = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.SagaRetentionPeriod));
	}

	[Fact]
	public void FailWhenCleanupIntervalIsZero_AndCleanupEnabled()
	{
		// Arrange
		var options = new SagaOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.Zero,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.CleanupInterval));
	}

	[Fact]
	public void SucceedWhenCleanupIntervalIsZero_ButCleanupDisabled()
	{
		// Arrange
		var options = new SagaOptions
		{
			EnableAutomaticCleanup = false,
			CleanupInterval = TimeSpan.Zero,
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
		var options = new SagaOptions
		{
			MaxConcurrency = 0,
			DefaultTimeout = TimeSpan.Zero,
			SagaRetentionPeriod = TimeSpan.Zero,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(SagaOptions.MaxConcurrency));
		result.FailureMessage.ShouldContain(nameof(SagaOptions.DefaultTimeout));
		result.FailureMessage.ShouldContain(nameof(SagaOptions.SagaRetentionPeriod));
	}
}
