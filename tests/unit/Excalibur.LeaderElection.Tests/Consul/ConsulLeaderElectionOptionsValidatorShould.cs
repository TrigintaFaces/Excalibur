// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>
/// Tests for <see cref="ConsulLeaderElectionOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ConsulLeaderElectionOptionsValidatorShould
{
	private readonly ConsulLeaderElectionOptionsValidator _sut = new();

	[Fact]
	public void SucceedWhenLockDelayIsLessThanSessionTTL()
	{
		// Arrange
		var options = new ConsulLeaderElectionOptions
		{
			LockDelay = TimeSpan.FromSeconds(10),
			SessionTTL = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenLockDelayEqualsSessionTTL()
	{
		// Arrange
		var options = new ConsulLeaderElectionOptions
		{
			LockDelay = TimeSpan.FromSeconds(30),
			SessionTTL = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("LockDelay");
		result.FailureMessage.ShouldContain("SessionTTL");
	}

	[Fact]
	public void FailWhenLockDelayExceedsSessionTTL()
	{
		// Arrange
		var options = new ConsulLeaderElectionOptions
		{
			LockDelay = TimeSpan.FromSeconds(60),
			SessionTTL = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("LockDelay");
		result.FailureMessage.ShouldContain("must be less than");
	}

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		// Arrange — defaults: LockDelay=15s, SessionTTL=30s
		var options = new ConsulLeaderElectionOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void SucceedWithMinimalLockDelay()
	{
		// Arrange
		var options = new ConsulLeaderElectionOptions
		{
			LockDelay = TimeSpan.FromMilliseconds(1),
			SessionTTL = TimeSpan.FromSeconds(10),
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenLockDelayIsZeroAndSessionTTLIsZero()
	{
		// Arrange — 0 >= 0 should fail
		var options = new ConsulLeaderElectionOptions
		{
			LockDelay = TimeSpan.Zero,
			SessionTTL = TimeSpan.Zero,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void AcceptNamedOptions()
	{
		// Arrange
		var options = new ConsulLeaderElectionOptions
		{
			LockDelay = TimeSpan.FromSeconds(5),
			SessionTTL = TimeSpan.FromSeconds(30),
		};

		// Act
		var result = _sut.Validate("named", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
