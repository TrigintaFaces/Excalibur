// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	// passes validation yet guarantees a split-brain window: the renewal loop self-demotes ~Renew+Grace
	// after the last success while the lease expires at LeaseDuration. The validator MUST also enforce
	// the cross-property rule Renew + Grace (+ skew margin) < Lease.

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
