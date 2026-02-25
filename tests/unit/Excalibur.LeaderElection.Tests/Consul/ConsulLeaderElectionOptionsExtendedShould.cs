// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>
/// Extended unit tests for <see cref="ConsulLeaderElectionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class ConsulLeaderElectionOptionsExtendedShould
{
	[Fact]
	public void HaveDefaultConsulAddress()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.ConsulAddress.ShouldBe("http://localhost:8500");
	}

	[Fact]
	public void HaveDefaultKeyPrefix()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.KeyPrefix.ShouldBe("excalibur/leader-election");
	}

	[Fact]
	public void HaveDefaultSessionTTL()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.SessionTTL.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultLockDelay()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.LockDelay.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveNullDatacenterByDefault()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.Datacenter.ShouldBeNull();
	}

	[Fact]
	public void HaveNullTokenByDefault()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.Token.ShouldBeNull();
	}

	[Fact]
	public void HaveNullHealthCheckIdByDefault()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.HealthCheckId.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomConsulAddress()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { ConsulAddress = "http://consul.local:8500" };

		// Assert
		options.ConsulAddress.ShouldBe("http://consul.local:8500");
	}

	[Fact]
	public void AllowCustomDatacenter()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { Datacenter = "dc1" };

		// Assert
		options.Datacenter.ShouldBe("dc1");
	}

	[Fact]
	public void AllowCustomToken()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { Token = "my-secret-token" };

		// Assert
		options.Token.ShouldBe("my-secret-token");
	}

	[Fact]
	public void AllowCustomKeyPrefix()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { KeyPrefix = "my-app/leader" };

		// Assert
		options.KeyPrefix.ShouldBe("my-app/leader");
	}

	[Fact]
	public void AllowCustomSessionTTL()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { SessionTTL = TimeSpan.FromMinutes(1) };

		// Assert
		options.SessionTTL.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowCustomLockDelay()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { LockDelay = TimeSpan.FromSeconds(5) };

		// Assert
		options.LockDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AllowCustomHealthCheckId()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { HealthCheckId = "service:my-app" };

		// Assert
		options.HealthCheckId.ShouldBe("service:my-app");
	}

	[Fact]
	public void AllowCustomMaxRetryAttempts()
	{
		// Act
		var options = new ConsulLeaderElectionOptions { MaxRetryAttempts = 5 };

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void InheritFromLeaderElectionOptions()
	{
		// Act
		var options = new ConsulLeaderElectionOptions();

		// Assert - base class properties should be accessible
		options.InstanceId.ShouldNotBeNull();
		options.RenewInterval.ShouldBeGreaterThan(TimeSpan.Zero);
	}
}
