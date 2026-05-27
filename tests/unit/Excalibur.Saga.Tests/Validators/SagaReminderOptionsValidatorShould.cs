// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Reminders;

namespace Excalibur.Saga.Tests.Validators;

/// <summary>
/// Unit tests for <see cref="SagaReminderOptionsValidator"/>.
/// Sprint 833 bd-1he43e: ValidateOnStart audit.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaReminderOptionsValidatorShould
{
    private readonly SagaReminderOptionsValidator _validator = new();

    #region Success Cases

    [Fact]
    public void SucceedForDefaultOptions()
    {
        // Arrange
        var options = new SagaReminderOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedForCustomValidOptions()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromMinutes(10),
            MinimumDelay = TimeSpan.FromSeconds(5),
            MaximumDelay = TimeSpan.FromDays(7),
            MaxRemindersPerSaga = 50,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedWhenDefaultDelayEqualsMinimumDelay()
    {
        // Arrange — boundary: DefaultDelay exactly at minimum
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromSeconds(1),
            MinimumDelay = TimeSpan.FromSeconds(1),
            MaximumDelay = TimeSpan.FromDays(30),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedWhenDefaultDelayEqualsMaximumDelay()
    {
        // Arrange — boundary: DefaultDelay exactly at maximum
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromDays(30),
            MinimumDelay = TimeSpan.FromSeconds(1),
            MaximumDelay = TimeSpan.FromDays(30),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    #endregion

    #region DefaultDelay Validation

    [Fact]
    public void FailWhenDefaultDelayIsZero()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.Zero,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.DefaultDelay));
    }

    [Fact]
    public void FailWhenDefaultDelayIsNegative()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromMinutes(-1),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.DefaultDelay));
    }

    #endregion

    #region MaxRemindersPerSaga Validation

    [Fact]
    public void FailWhenMaxRemindersPerSagaIsZero()
    {
        // Arrange
        var options = new SagaReminderOptions { MaxRemindersPerSaga = 0 };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaxRemindersPerSaga));
    }

    [Fact]
    public void FailWhenMaxRemindersPerSagaIsNegative()
    {
        // Arrange
        var options = new SagaReminderOptions { MaxRemindersPerSaga = -1 };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaxRemindersPerSaga));
    }

    #endregion

    #region MinimumDelay Validation

    [Fact]
    public void FailWhenMinimumDelayIsZero()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            MinimumDelay = TimeSpan.Zero,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MinimumDelay));
    }

    [Fact]
    public void FailWhenMinimumDelayIsNegative()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            MinimumDelay = TimeSpan.FromSeconds(-1),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MinimumDelay));
    }

    #endregion

    #region MaximumDelay Validation

    [Fact]
    public void FailWhenMaximumDelayIsZero()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            MaximumDelay = TimeSpan.Zero,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaximumDelay));
    }

    [Fact]
    public void FailWhenMaximumDelayIsNegative()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            MaximumDelay = TimeSpan.FromDays(-1),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaximumDelay));
    }

    #endregion

    #region Cross-Property: MinimumDelay < MaximumDelay

    [Fact]
    public void FailWhenMinimumDelayEqualsMaximumDelay()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromMinutes(5),
            MinimumDelay = TimeSpan.FromMinutes(5),
            MaximumDelay = TimeSpan.FromMinutes(5),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MinimumDelay));
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaximumDelay));
    }

    [Fact]
    public void FailWhenMinimumDelayExceedsMaximumDelay()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromMinutes(5),
            MinimumDelay = TimeSpan.FromDays(7),
            MaximumDelay = TimeSpan.FromDays(1),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MinimumDelay));
    }

    #endregion

    #region Cross-Property: DefaultDelay in [MinimumDelay, MaximumDelay]

    [Fact]
    public void FailWhenDefaultDelayIsBelowMinimumDelay()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromMilliseconds(500),
            MinimumDelay = TimeSpan.FromSeconds(1),
            MaximumDelay = TimeSpan.FromDays(30),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.DefaultDelay));
        result.FailureMessage.ShouldContain("between");
    }

    [Fact]
    public void FailWhenDefaultDelayExceedsMaximumDelay()
    {
        // Arrange
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.FromDays(60),
            MinimumDelay = TimeSpan.FromSeconds(1),
            MaximumDelay = TimeSpan.FromDays(30),
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.DefaultDelay));
        result.FailureMessage.ShouldContain("between");
    }

    #endregion

    #region Multiple Failures

    [Fact]
    public void ReportMultipleFailures()
    {
        // Arrange — all values invalid
        var options = new SagaReminderOptions
        {
            DefaultDelay = TimeSpan.Zero,
            MinimumDelay = TimeSpan.Zero,
            MaximumDelay = TimeSpan.Zero,
            MaxRemindersPerSaga = 0,
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.DefaultDelay));
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MinimumDelay));
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaximumDelay));
        result.FailureMessage.ShouldContain(nameof(SagaReminderOptions.MaxRemindersPerSaga));
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
