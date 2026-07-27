// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// Tests for <see cref="KubernetesLeaderElectionOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class KubernetesLeaderElectionOptionsValidatorShould
{
	private readonly KubernetesLeaderElectionOptionsValidator _sut = new();

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		// Arrange — defaults: LeaseDuration=15s, RenewInterval=5000ms, GracePeriod=5s
		var options = new KubernetesLeaderElectionOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenRenewIntervalExceedsLeaseDuration()
	{
		// Arrange — 20000ms > 15s * 1000 = 15000ms
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromMilliseconds(20000),
			LeaseDuration = TimeSpan.FromSeconds(15),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewInterval");
		result.FailureMessage.ShouldContain("LeaseDuration");
	}

	[Fact]
	public void FailWhenRenewIntervalEqualsLeaseDuration()
	{
		// Arrange — 15000ms == 15s * 1000
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromMilliseconds(15000),
			LeaseDuration = TimeSpan.FromSeconds(15),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewInterval");
	}

	[Fact]
	public void FailWhenGracePeriodExceedsLeaseDuration()
	{
		// Arrange — GracePeriod=20s > LeaseDuration=15s
		var options = new KubernetesLeaderElectionOptions
		{
			GracePeriod = TimeSpan.FromSeconds(20),
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromMilliseconds(5000), // valid
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("GracePeriod");
		result.FailureMessage.ShouldContain("LeaseDuration");
	}

	[Fact]
	public void FailWhenGracePeriodEqualsLeaseDuration()
	{
		// Arrange — GracePeriod=15s == LeaseDuration=15s
		var options = new KubernetesLeaderElectionOptions
		{
			GracePeriod = TimeSpan.FromSeconds(15),
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromMilliseconds(5000), // valid
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("GracePeriod");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void SucceedWhenRenewIntervalIsWellBelowLeaseDuration()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromMilliseconds(1000),
			LeaseDuration = TimeSpan.FromSeconds(30),
			GracePeriod = TimeSpan.FromSeconds(5),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailOnRenewIntervalBeforeCheckingGracePeriod()
	{
		// Arrange — both violations, but renew check comes first
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromMilliseconds(20000),
			LeaseDuration = TimeSpan.FromSeconds(15),
			GracePeriod = TimeSpan.FromSeconds(20),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert — should fail on renew interval first
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewInterval");
	}

	[Fact]
	public void AcceptNamedOptions()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions();

		// Act
		var result = _sut.Validate("my-named-options", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void IncludeLeaseDurationAndRenewIntervalInFailureMessage()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromSeconds(20),
			LeaseDuration = TimeSpan.FromSeconds(10),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert — the failure names both properties and shows their TimeSpan values.
		result.FailureMessage.ShouldContain(nameof(KubernetesLeaderElectionOptions.RenewInterval));
		result.FailureMessage.ShouldContain(nameof(KubernetesLeaderElectionOptions.LeaseDuration));
		result.FailureMessage.ShouldContain(TimeSpan.FromSeconds(10).ToString());
		result.FailureMessage.ShouldContain(TimeSpan.FromSeconds(20).ToString());
	}

	// --- Split-brain self-demotion sum-invariant (bgo7g3) ---
	// The derived Kubernetes validator MUST enforce the base combined invariant
	// RenewInterval + GracePeriod + clock-skew < LeaseDuration, not only the two
	// pairwise (< LeaseDuration) checks. A config that passes BOTH pairwise checks but
	// violates the sum still guarantees a split-brain overlap window, so it must fail.

	[Fact]
	public void FailWhenSelfDemotionDeadlineReachesLeaseDurationDespitePairwiseChecksPassing()
	{
		// Arrange — Renew=5s, Grace=5s, Lease=8s.
		// Pairwise: 5s < 8s (renew) and 5s < 8s (grace) both PASS, yet the self-demotion
		// deadline 5s + 5s + 1s clock-skew = 11s >= 8s lease → guaranteed split-brain window.
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromSeconds(5),
			GracePeriod = TimeSpan.FromSeconds(5),
			LeaseDuration = TimeSpan.FromSeconds(8),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert — must reject; the pairwise-only validator (pre-fix) accepts this.
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KubernetesLeaderElectionOptions.LeaseDuration));
		result.FailureMessage.ShouldContain("split-brain");
	}

	[Fact]
	public void FailWhenSelfDemotionDeadlineExactlyEqualsLeaseDuration()
	{
		// Arrange — Renew=4s, Grace=5s, Lease=10s. Pairwise both pass (4<10, 5<10).
		// Sum with clock-skew: 4s + 5s + 1s = 10s, which is NOT strictly less than the
		// 10s lease → boundary case must still fail (self-demotion at lease expiry).
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromSeconds(4),
			GracePeriod = TimeSpan.FromSeconds(5),
			LeaseDuration = TimeSpan.FromSeconds(10),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KubernetesLeaderElectionOptions.LeaseDuration));
	}

	[Fact]
	public void SucceedWhenSelfDemotionDeadlineIsStrictlyBelowLeaseDuration()
	{
		// Arrange — Renew=4s, Grace=4s, Lease=10s. Sum 4s + 4s + 1s = 9s < 10s → valid.
		var options = new KubernetesLeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromSeconds(4),
			GracePeriod = TimeSpan.FromSeconds(4),
			LeaseDuration = TimeSpan.FromSeconds(10),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- Retry/timing lower-bound checks (vffppp follow-up) ---
	// The derived validator restores the lower-bound guards dropped when the int-ms knobs
	// moved to TimeSpan (which lost their [Range] attributes): RetryInterval > 0,
	// MaxRetries >= 0, MaxRetryDelay >= 0. Rejected at ValidateOnStart rather than surfacing
	// as a runtime Task.Delay ArgumentOutOfRangeException. RED if any guard is removed.

	[Fact]
	public void FailWhenRetryIntervalIsZero()
	{
		// Arrange — lease/renew/grace left at valid defaults so the base validator passes
		// and the derived RetryInterval lower-bound check is reached.
		var options = new KubernetesLeaderElectionOptions
		{
			RetryInterval = TimeSpan.Zero,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(LeaderElectionOptions.RetryInterval));
	}

	[Fact]
	public void FailWhenRetryIntervalIsNegative()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions
		{
			RetryInterval = TimeSpan.FromSeconds(-1),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(LeaderElectionOptions.RetryInterval));
	}

	[Fact]
	public void FailWhenMaxRetriesIsNegative()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions
		{
			MaxRetries = -1,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KubernetesLeaderElectionOptions.MaxRetries));
	}

	[Fact]
	public void FailWhenMaxRetryDelayIsNegative()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions
		{
			MaxRetryDelay = TimeSpan.FromSeconds(-1),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KubernetesLeaderElectionOptions.MaxRetryDelay));
	}

	[Fact]
	public void SucceedWhenRetryKnobsAtValidBoundaries()
	{
		// Arrange — MaxRetries=0 and MaxRetryDelay=0 are the valid lower boundaries (non-negative),
		// RetryInterval strictly positive. Guards non-vacuity of the failure tests above.
		var options = new KubernetesLeaderElectionOptions
		{
			RetryInterval = TimeSpan.FromMilliseconds(1),
			MaxRetries = 0,
			MaxRetryDelay = TimeSpan.Zero,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
