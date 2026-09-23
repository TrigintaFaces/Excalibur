// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerOptionsValidator"/>.
/// Sprint 561 S561.53: IValidateOptions implementation tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
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
	public void FailWhenConsecutiveFailureThresholdIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 0 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.ConsecutiveFailureThreshold));
	}

	[Fact]
	public void FailWhenMinimumThroughputIsBelowTwo()
	{
		// This arm is the replacement for a check the ratio-based provider used to make at
		// construction. Moving it here is only sound if the validator actually rejects the value,
		// so without this arm the constraint would have been deleted rather than relocated.

		// Arrange
		var options = new CircuitBreakerOptions { MinimumThroughput = 1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.MinimumThroughput));
	}

	[Fact]
	public void AcceptConsecutiveFailureThresholdOfOneWithAValidThroughput()
	{
		// Liveness for the arm above: the validator must reject only what is genuinely invalid.
		// Opening on the first consecutive failure is legal, and a validator that refused it would
		// pass the rejection arms while making a supported configuration unusable.

		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = 1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenConsecutiveFailureThresholdIsNegative()
	{
		// Arrange
		var options = new CircuitBreakerOptions { ConsecutiveFailureThreshold = -1 };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.ConsecutiveFailureThreshold));
	}


	[Fact]
	public void FailWhenOpenDurationIsZero()
	{
		// Arrange
		var options = new CircuitBreakerOptions { BreakDuration = TimeSpan.Zero };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
	}

	[Fact]
	public void FailWhenOpenDurationIsNegative()
	{
		// Arrange
		var options = new CircuitBreakerOptions { BreakDuration = TimeSpan.FromSeconds(-1) };

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
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
	public void FailWhenOperationTimeoutExceedsOpenDuration()
	{
		// Arrange - OperationTimeout >= BreakDuration should fail
		var options = new CircuitBreakerOptions
		{
			OperationTimeout = TimeSpan.FromSeconds(60),
			BreakDuration = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
	}

	[Fact]
	public void FailWhenOperationTimeoutEqualsOpenDuration()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			OperationTimeout = TimeSpan.FromSeconds(30),
			BreakDuration = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
	}


	[Fact]
	public void SucceedWhenSuccessThresholdEqualsMaxHalfOpenTests()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
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
			ConsecutiveFailureThreshold = 0,
			BreakDuration = TimeSpan.Zero,
			OperationTimeout = TimeSpan.Zero,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.ConsecutiveFailureThreshold));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.OperationTimeout));
	}

	// ---------------------------------------------------------------------------------------------
	// PROVIDER-PARITY BOUNDS.
	//
	// These values are forwarded verbatim into the resilience provider's own options, which validate
	// them again with narrower ranges than "positive". Anything this validator admits but the provider
	// does not produces a startup that passes our checks and then throws from inside the provider when
	// the pipeline is built -- a configuration error surfacing at the worst possible moment, in a place
	// that names neither the option nor the call that set it.
	//
	// The bounds were read from the provider's own [Range] attributes rather than recalled:
	//   SamplingDuration  500ms .. 1d      BreakDuration  500ms .. 1d      OperationTimeout  10ms .. 1d
	//
	// WHICH ARMS ARE NON-VACUOUS, checked against the committed validator rather than asserted. The
	// previous version checked SamplingDuration against a 500ms floor it already had, BreakDuration and
	// OperationTimeout only for "> zero", and no upper bound at all:
	//   BreakDuration below 500ms   NEW  -- was accepted (only "<= Zero" was rejected)
	//   any window above 1 day      NEW  -- no ceiling existed for any of the three
	//   SamplingDuration floor      PRE-EXISTING -- kept as a regression lock, NOT evidence of this change
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData(1)]     // the value the conformance kit used while nothing bounded this property
	[InlineData(100)]
	[InlineData(499)]
	public void FailWhenBreakDurationIsBelowTheProviderFloor(int milliseconds)
	{
		var options = new CircuitBreakerOptions { BreakDuration = TimeSpan.FromMilliseconds(milliseconds) };

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
	}

	[Fact]
	public void FailWhenSamplingDurationIsBelowTheProviderFloor()
	{
		// PRE-EXISTING bound, kept as a regression lock. This arm passed before the provider-parity
		// change too, so it is not evidence for it -- labelled rather than deleted, because a reader
		// counting green arms would otherwise credit it to the wrong change.
		var options = new CircuitBreakerOptions { SamplingDuration = TimeSpan.FromMilliseconds(100) };

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.SamplingDuration));
	}

	[Fact]
	public void FailWhenSamplingDurationExceedsTheProviderCeiling()
	{
		// NEW. No upper bound existed for any window, so a 2-day sampling window was accepted here and
		// rejected by the provider when the pipeline was built.
		var options = new CircuitBreakerOptions { SamplingDuration = TimeSpan.FromDays(2) };

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.SamplingDuration));
	}

	[Fact]
	public void FailWhenAWindowExceedsTheProviderCeiling()
	{
		// The upper bound is real too, and nothing checked it at all before: a two-day break was
		// accepted here and rejected by the provider.
		var options = new CircuitBreakerOptions { BreakDuration = TimeSpan.FromDays(2) };

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CircuitBreakerOptions.BreakDuration));
	}

	[Fact]
	public void AcceptTheWindowsExactlyAtTheProviderFloor()
	{
		// LIVENESS. Without this, tightening the bound to reject everything would satisfy all three
		// arms above -- and 500ms is the exact boundary the provider admits, so an off-by-one in the
		// comparison (< versus <=) is caught here rather than by a consumer.
		var options = new CircuitBreakerOptions
		{
			SamplingDuration = TimeSpan.FromMilliseconds(500),
			BreakDuration = TimeSpan.FromMilliseconds(500),
			OperationTimeout = TimeSpan.FromMilliseconds(10),
		};

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}
}
