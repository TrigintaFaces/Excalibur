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
			RenewIntervalMilliseconds = 20000,
			LeaseDurationSeconds = 15,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewIntervalMilliseconds");
		result.FailureMessage.ShouldContain("LeaseDurationSeconds");
	}

	[Fact]
	public void FailWhenRenewIntervalEqualsLeaseDuration()
	{
		// Arrange — 15000ms == 15s * 1000
		var options = new KubernetesLeaderElectionOptions
		{
			RenewIntervalMilliseconds = 15000,
			LeaseDurationSeconds = 15,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewIntervalMilliseconds");
	}

	[Fact]
	public void FailWhenGracePeriodExceedsLeaseDuration()
	{
		// Arrange — GracePeriod=20s > LeaseDuration=15s
		var options = new KubernetesLeaderElectionOptions
		{
			GracePeriodSeconds = 20,
			LeaseDurationSeconds = 15,
			RenewIntervalMilliseconds = 5000, // valid
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("GracePeriodSeconds");
		result.FailureMessage.ShouldContain("LeaseDurationSeconds");
	}

	[Fact]
	public void FailWhenGracePeriodEqualsLeaseDuration()
	{
		// Arrange — GracePeriod=15s == LeaseDuration=15s
		var options = new KubernetesLeaderElectionOptions
		{
			GracePeriodSeconds = 15,
			LeaseDurationSeconds = 15,
			RenewIntervalMilliseconds = 5000, // valid
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("GracePeriodSeconds");
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
			RenewIntervalMilliseconds = 1000,
			LeaseDurationSeconds = 30,
			GracePeriodSeconds = 5,
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
			RenewIntervalMilliseconds = 20000,
			LeaseDurationSeconds = 15,
			GracePeriodSeconds = 20,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert — should fail on renew interval first
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RenewIntervalMilliseconds");
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
	public void IncludeLeaseDurationInMillisecondsInFailureMessage()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions
		{
			RenewIntervalMilliseconds = 20000,
			LeaseDurationSeconds = 10,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert — should show "10s = 10000ms"
		result.FailureMessage.ShouldContain("10000ms");
	}
}
