// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Unit tests for <see cref="CdcProcessingOptionsValidator"/>.
/// Sprint 561 S561.53: IValidateOptions implementation tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CdcProcessingOptionsValidatorShould
{
	private readonly CdcProcessingOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new CdcProcessingOptions();

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
	public void FailWhenPollingIntervalIsZero()
	{
		// Arrange
		var options = new CdcProcessingOptions { PollingInterval = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.PollingInterval));
	}

	[Fact]
	public void FailWhenPollingIntervalIsNegative()
	{
		// Arrange
		var options = new CdcProcessingOptions { PollingInterval = TimeSpan.FromSeconds(-1) };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.PollingInterval));
	}

	[Fact]
	public void FailWhenDrainTimeoutIsLessThanOrEqualToPollingInterval()
	{
		// Arrange - DrainTimeoutSeconds=5 yields DrainTimeout=5s, PollingInterval=5s
		var options = new CdcProcessingOptions
		{
			PollingInterval = TimeSpan.FromSeconds(5),
			DrainTimeoutSeconds = 5,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.DrainTimeoutSeconds));
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.PollingInterval));
	}

	[Fact]
	public void SucceedWhenDrainTimeoutIsGreaterThanPollingInterval()
	{
		// Arrange
		var options = new CdcProcessingOptions
		{
			PollingInterval = TimeSpan.FromSeconds(5),
			DrainTimeoutSeconds = 30,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenUnhealthyThresholdIsZero()
	{
		// Arrange
		var options = new CdcProcessingOptions { UnhealthyThreshold = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.UnhealthyThreshold));
	}

	[Fact]
	public void FailWhenUnhealthyThresholdIsNegative()
	{
		// Arrange
		var options = new CdcProcessingOptions { UnhealthyThreshold = -1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.UnhealthyThreshold));
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		// Arrange
		var options = new CdcProcessingOptions
		{
			PollingInterval = TimeSpan.Zero,
			UnhealthyThreshold = 0,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.PollingInterval));
		result.FailureMessage.ShouldContain(nameof(CdcProcessingOptions.UnhealthyThreshold));
	}
}
