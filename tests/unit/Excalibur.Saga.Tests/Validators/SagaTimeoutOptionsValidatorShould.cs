// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Tests.Validators;

/// <summary>
/// Unit tests for <see cref="SagaTimeoutOptionsValidator"/>.
/// Sprint 833 bd-1he43e: ValidateOnStart audit.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaTimeoutOptionsValidatorShould
{
    private readonly SagaTimeoutOptionsValidator _validator = new();

    #region Success Cases

    [Fact]
    public void SucceedForDefaultOptions()
    {
        // Arrange
        var options = new SagaTimeoutOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedForMinimumValidPollInterval()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(100),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedForLargePollInterval()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            PollInterval = TimeSpan.FromMinutes(10),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedForMinimumBatchSize()
    {
        // Arrange
        var options = new SagaTimeoutOptions { BatchSize = 1 };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    #endregion

    #region PollInterval Validation

    [Fact]
    public void FailWhenPollIntervalIsBelowMinimum()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(50),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.PollInterval));
    }

    [Fact]
    public void FailWhenPollIntervalIsZero()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            PollInterval = TimeSpan.Zero,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.PollInterval));
    }

    [Fact]
    public void FailWhenPollIntervalIsNegative()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            PollInterval = TimeSpan.FromSeconds(-1),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.PollInterval));
    }

    #endregion

    #region BatchSize Validation

    [Fact]
    public void FailWhenBatchSizeIsZero()
    {
        // Arrange
        var options = new SagaTimeoutOptions { BatchSize = 0 };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.BatchSize));
    }

    [Fact]
    public void FailWhenBatchSizeIsNegative()
    {
        // Arrange
        var options = new SagaTimeoutOptions { BatchSize = -5 };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.BatchSize));
    }

    #endregion

    #region ShutdownTimeout Validation

    [Fact]
    public void FailWhenShutdownTimeoutIsZero()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            ShutdownTimeout = TimeSpan.Zero,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.ShutdownTimeout));
    }

    [Fact]
    public void FailWhenShutdownTimeoutIsNegative()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            ShutdownTimeout = TimeSpan.FromSeconds(-10),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.ShutdownTimeout));
    }

    #endregion

    #region Multiple Failures

    [Fact]
    public void ReportMultipleFailures()
    {
        // Arrange
        var options = new SagaTimeoutOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(10),
            BatchSize = 0,
            ShutdownTimeout = TimeSpan.Zero,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.PollInterval));
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.BatchSize));
        result.FailureMessage.ShouldContain(nameof(SagaTimeoutOptions.ShutdownTimeout));
    }

    #endregion

    #region Null Guard

    [Fact]
    public void ThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
    }

    #endregion
}
