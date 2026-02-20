// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="OutboxOptionsValidator"/>.
/// Sprint 563 S563.57: IValidateOptions validator tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxOptionsValidatorShould
{
	private readonly OutboxOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new OutboxOptions();

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
	public void FailWhenPublishBatchSizeIsZero()
	{
		// Arrange
		var options = new OutboxOptions { PublishBatchSize = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.PublishBatchSize));
	}

	[Fact]
	public void FailWhenPublishPollingIntervalIsZero()
	{
		// Arrange
		var options = new OutboxOptions { PublishPollingInterval = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.PublishPollingInterval));
	}

	[Fact]
	public void FailWhenMaxRetriesIsNegative()
	{
		// Arrange
		var options = new OutboxOptions { MaxRetries = -1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.MaxRetries));
	}

	[Fact]
	public void SucceedWhenMaxRetriesIsZero()
	{
		// Arrange - zero retries is valid (no retries)
		var options = new OutboxOptions { MaxRetries = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenRetryDelayIsZero()
	{
		// Arrange
		var options = new OutboxOptions { RetryDelay = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.RetryDelay));
	}

	[Fact]
	public void FailWhenCleanupAgeIsZero()
	{
		// Arrange
		var options = new OutboxOptions { CleanupAge = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.CleanupAge));
	}

	[Fact]
	public void FailWhenCleanupIntervalIsZero()
	{
		// Arrange
		var options = new OutboxOptions { CleanupInterval = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.CleanupInterval));
	}

	[Fact]
	public void FailWhenMaxRetryDelayLessThanRetryDelay_WithExponentialBackoff()
	{
		// Arrange
		var options = new OutboxOptions
		{
			EnableExponentialRetryBackoff = true,
			RetryDelay = TimeSpan.FromMinutes(10),
			MaxRetryDelay = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.MaxRetryDelay));
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.RetryDelay));
	}

	[Fact]
	public void SucceedWhenMaxRetryDelayEqualsRetryDelay_WithExponentialBackoff()
	{
		// Arrange
		var options = new OutboxOptions
		{
			EnableExponentialRetryBackoff = true,
			RetryDelay = TimeSpan.FromMinutes(5),
			MaxRetryDelay = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenMinPollingIntervalExceedsPublishPollingInterval_WithAdaptivePolling()
	{
		// Arrange
		var options = new OutboxOptions
		{
			EnableAdaptivePolling = true,
			MinPollingInterval = TimeSpan.FromSeconds(10),
			PublishPollingInterval = TimeSpan.FromSeconds(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.MinPollingInterval));
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.PublishPollingInterval));
	}

	[Fact]
	public void FailWhenBackoffMultiplierIsOneOrLess_WithAdaptivePolling()
	{
		// Arrange
		var options = new OutboxOptions
		{
			EnableAdaptivePolling = true,
			AdaptivePollingBackoffMultiplier = 1.0,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.AdaptivePollingBackoffMultiplier));
	}

	[Fact]
	public void SucceedWhenAdaptivePollingDisabled_WithInvalidSettings()
	{
		// Arrange - invalid adaptive polling settings should not matter when disabled
		var options = new OutboxOptions
		{
			EnableAdaptivePolling = false,
			MinPollingInterval = TimeSpan.FromSeconds(999),
			AdaptivePollingBackoffMultiplier = 0.5,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWhenExponentialBackoffDisabled_WithInvalidMaxRetryDelay()
	{
		// Arrange - MaxRetryDelay < RetryDelay should not matter when exponential backoff is disabled
		var options = new OutboxOptions
		{
			EnableExponentialRetryBackoff = false,
			RetryDelay = TimeSpan.FromMinutes(10),
			MaxRetryDelay = TimeSpan.FromMinutes(1),
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
		var options = new OutboxOptions
		{
			PublishBatchSize = 0,
			PublishPollingInterval = TimeSpan.Zero,
			RetryDelay = TimeSpan.Zero,
			CleanupAge = TimeSpan.Zero,
			CleanupInterval = TimeSpan.Zero,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.PublishBatchSize));
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.PublishPollingInterval));
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.RetryDelay));
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.CleanupAge));
		result.FailureMessage.ShouldContain(nameof(OutboxOptions.CleanupInterval));
	}
}
