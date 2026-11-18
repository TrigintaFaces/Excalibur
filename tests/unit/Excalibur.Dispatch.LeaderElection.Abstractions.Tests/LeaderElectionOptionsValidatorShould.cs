// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaderElectionOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderElectionOptionsValidatorShould : UnitTestBase
{
	private readonly LeaderElectionOptionsValidator _validator = new();

	[Theory]
	[InlineData(nameof(LeaderElectionOptions.LeaseDuration), 0L)]
	[InlineData(nameof(LeaderElectionOptions.LeaseDuration), -1L)]
	[InlineData(nameof(LeaderElectionOptions.LeaseDuration), -10000000L)]
	[InlineData(nameof(LeaderElectionOptions.RenewInterval), 0L)]
	[InlineData(nameof(LeaderElectionOptions.RenewInterval), -1L)]
	[InlineData(nameof(LeaderElectionOptions.RenewInterval), -10000000L)]
	[InlineData(nameof(LeaderElectionOptions.RetryInterval), 0L)]
	[InlineData(nameof(LeaderElectionOptions.RetryInterval), -1L)]
	[InlineData(nameof(LeaderElectionOptions.RetryInterval), -10000000L)]
	[InlineData(nameof(LeaderElectionOptions.GracePeriod), 0L)]
	[InlineData(nameof(LeaderElectionOptions.GracePeriod), -1L)]
	[InlineData(nameof(LeaderElectionOptions.GracePeriod), -10000000L)]
	public void RejectNonPositiveTimingWithItsConfigurationName(string property, long ticks)
	{
		var options = new LeaderElectionOptions();
		var value = TimeSpan.FromTicks(ticks);
		switch (property)
		{
			case nameof(LeaderElectionOptions.LeaseDuration): options.LeaseDuration = value; break;
			case nameof(LeaderElectionOptions.RenewInterval): options.RenewInterval = value; break;
			case nameof(LeaderElectionOptions.RetryInterval): options.RetryInterval = value; break;
			case nameof(LeaderElectionOptions.GracePeriod): options.GracePeriod = value; break;
			default: throw new ArgumentOutOfRangeException(nameof(property));
		}
		var result = _validator.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(property);
	}

	[Fact]
	public void RejectAnOverflowingTimingSumAsValidationFailure()
	{
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.MaxValue,
			RenewInterval = TimeSpan.FromTicks(1),
			GracePeriod = TimeSpan.MaxValue - TimeSpan.FromTicks(2),
		};
		var result = _validator.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(LeaderElectionOptions.LeaseDuration));
	}

	[Theory]
	[InlineData(-1L, true)]
	[InlineData(0L, false)]
	[InlineData(1L, false)]
	public void PreserveTheStrictHeadroomBoundary(long tickOffset, bool expectedSuccess)
	{
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(5),
			GracePeriod = TimeSpan.FromSeconds(9) + TimeSpan.FromTicks(tickOffset),
		};
		_validator.Validate(null, options).Succeeded.ShouldBe(expectedSuccess);
	}

	[Fact]
	public void Validate_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void Validate_WithValidDefaults_ReturnsSuccess()
	{
		// Arrange
		var options = new LeaderElectionOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_RenewIntervalGreaterThanOrEqualLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(15),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewInterval");
		result.FailureMessage.ShouldContain("LeaseDuration");
	}

	[Fact]
	public void Validate_RenewIntervalGreaterThanLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(10),
			RenewInterval = TimeSpan.FromSeconds(20),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_GracePeriodGreaterThanOrEqualLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			GracePeriod = TimeSpan.FromSeconds(15),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("GracePeriod");
		result.FailureMessage.ShouldContain("LeaseDuration");
	}

	[Fact]
	public void Validate_GracePeriodGreaterThanLeaseDuration_Fails()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(10),
			GracePeriod = TimeSpan.FromSeconds(20),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_RenewIntervalLessThanLeaseDuration_Succeeds()
	{
		// Arrange
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			GracePeriod = TimeSpan.FromSeconds(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_WithNameParameter_StillValidates()
	{
		// Arrange
		var options = new LeaderElectionOptions();

		// Act
		var result = _validator.Validate("custom-name", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- ol729k (Lane C, AC-C5/C6): cross-property split-brain-window rule ---
	// The pre-fix validator only checks RenewInterval and GracePeriod individually against
	// LeaseDuration. A config where each is individually < LeaseDuration but their SUM exceeds it
	// passes validation despite insufficient configured headroom. The validator enforces the
	// cross-property rule Renew + Grace (+ reserved margin) < Lease. These arithmetic tests do not
	// establish provider runtime deadlines or prove exclusive ownership.

	[Fact]
	public void Validate_RenewPlusGracePeriodExceedsLeaseDuration_Fails()
	{
		// Arrange -- the exact ol729k split-brain config (AC-C5): Renew=10s, Grace=8s, Lease=15s.
		// Each individual property is < LeaseDuration, so NEITHER per-property rule fires; the only
		// way this fails is the new cross-property (sum) rule. 10 + 8 = 18s > 15s -> overlap window.
		// Non-vacuity: RED on the current per-property-only validator (returns Success for this config).
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(10),
			GracePeriod = TimeSpan.FromSeconds(8),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert -- must fail; sum (18s) exceeds the lease (15s) regardless of the chosen skew margin
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_RenewIntervalAndGracePeriodIndividuallyBelowLease_ButSumAtLease_Fails()
	{
		// Arrange -- boundary of the cross-property rule: each property < Lease, sum == Lease.
		// Renew=8s + Grace=7s = 15s == Lease 15s. A positive skew margin (or a strict "< Lease" rule)
		// must reject this -- a zero-margin self-demotion deadline coincides with lease expiry.
		// Non-vacuity: RED on the current validator (both individual checks pass -> Success).
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(8),
			GracePeriod = TimeSpan.FromSeconds(7),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ShippedDefaults_SatisfyCrossPropertyMargin()
	{
		// Arrange -- AC-C6: the shipped defaults (Lease=15s, Renew=5s, Grace=5s; sum 10s) must remain
		// valid with a positive margin (10s + skew < 15s) AFTER the cross-property rule is added.
		// Guards the fix against over-tightening (rejecting the shipped defaults).
		var options = new LeaderElectionOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
		(options.RenewInterval + options.GracePeriod).ShouldBeLessThan(options.LeaseDuration);
	}
}
