// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckOptionsValidator"/>.
/// Sprint 563 S563.55: IValidateOptions validator tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ClaimCheck")]
public sealed class ClaimCheckOptionsValidatorShould
{
	private readonly ClaimCheckOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new ClaimCheckOptions();

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
	public void FailWhenPayloadThresholdIsZero()
	{
		// Arrange
		var options = new ClaimCheckOptions { PayloadThreshold = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckOptions.PayloadThreshold));
	}

	[Fact]
	public void FailWhenPayloadThresholdIsNegative()
	{
		// Arrange
		var options = new ClaimCheckOptions { PayloadThreshold = -1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckOptions.PayloadThreshold));
	}

	[Fact]
	public void FailWhenCompressionThresholdExceedsPayloadThreshold()
	{
		// Arrange
		var options = new ClaimCheckOptions
		{
			PayloadThreshold = 1024,
			EnableCompression = true,
		};
		options.Compression.CompressionThreshold = 2048;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCompressionOptions.CompressionThreshold));
		result.FailureMessage.ShouldContain(nameof(ClaimCheckOptions.PayloadThreshold));
	}

	[Fact]
	public void FailWhenMinCompressionRatioExceedsOne()
	{
		// Arrange
		var options = new ClaimCheckOptions();
		options.Compression.MinCompressionRatio = 1.5;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCompressionOptions.MinCompressionRatio));
	}

	[Fact]
	public void FailWhenMinCompressionRatioIsNegative()
	{
		// Arrange
		var options = new ClaimCheckOptions();
		options.Compression.MinCompressionRatio = -0.1;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCompressionOptions.MinCompressionRatio));
	}

	[Fact]
	public void FailWhenCleanupIntervalIsZeroAndCleanupEnabled()
	{
		// Arrange
		var options = new ClaimCheckOptions();
		options.Cleanup.EnableCleanup = true;
		options.Cleanup.CleanupInterval = TimeSpan.Zero;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.CleanupInterval));
	}

	[Fact]
	public void FailWhenDefaultTtlIsZeroAndCleanupEnabled()
	{
		// Arrange
		var options = new ClaimCheckOptions();
		options.Cleanup.EnableCleanup = true;
		options.Cleanup.DefaultTtl = TimeSpan.Zero;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.DefaultTtl));
	}

	[Fact]
	public void FailWhenCleanupIntervalExceedsDefaultTtl()
	{
		// Arrange
		var options = new ClaimCheckOptions();
		options.Cleanup.EnableCleanup = true;
		options.Cleanup.CleanupInterval = TimeSpan.FromDays(30);
		options.Cleanup.DefaultTtl = TimeSpan.FromDays(1);

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.CleanupInterval));
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.DefaultTtl));
	}

	[Fact]
	public void FailWhenCleanupBatchSizeIsZero()
	{
		// Arrange
		var options = new ClaimCheckOptions();
		options.Cleanup.EnableCleanup = true;
		options.Cleanup.CleanupBatchSize = 0;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.CleanupBatchSize));
	}

	[Fact]
	public void SucceedWhenCleanupDisabledWithInvalidCleanupSettings()
	{
		// Arrange - invalid cleanup settings should not matter when cleanup is disabled
		var options = new ClaimCheckOptions();
		options.Cleanup.EnableCleanup = false;
		options.Cleanup.CleanupInterval = TimeSpan.Zero;
		options.Cleanup.DefaultTtl = TimeSpan.Zero;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWhenCompressionDisabledWithHighCompressionThreshold()
	{
		// Arrange - compression threshold doesn't matter when compression is disabled
		var options = new ClaimCheckOptions
		{
			PayloadThreshold = 1024,
		};
		options.Compression.EnableCompression = false;
		options.Compression.CompressionThreshold = 999999;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		// Arrange
		var options = new ClaimCheckOptions
		{
			PayloadThreshold = 0,
		};
		options.Compression.MinCompressionRatio = 2.0;
		options.Cleanup.EnableCleanup = true;
		options.Cleanup.CleanupInterval = TimeSpan.Zero;
		options.Cleanup.DefaultTtl = TimeSpan.Zero;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ClaimCheckOptions.PayloadThreshold));
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCompressionOptions.MinCompressionRatio));
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.CleanupInterval));
		result.FailureMessage.ShouldContain(nameof(ClaimCheckCleanupOptions.DefaultTtl));
	}
}
