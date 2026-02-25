// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Shouldly;

namespace Excalibur.LeaderElection.Tests.Consul;

[Trait("Category", "Unit")]
public class ConsulLeaderElectionOptionsShould
{
	[Fact]
	public void Inherit_From_LeaderElectionOptions()
	{
		// Arrange & Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		_ = options.ShouldBeAssignableTo<LeaderElectionOptions>();
	}

	[Fact]
	public void Have_Default_ConsulAddress()
	{
		// Arrange & Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.ConsulAddress.ShouldNotBeNullOrEmpty();
		options.ConsulAddress.ShouldBe("http://localhost:8500");
	}

	[Fact]
	public void Allow_Setting_ConsulAddress()
	{
		// Arrange
		const string consulAddress = "http://consul.example.com:8500";

		// Act
		var options = new ConsulLeaderElectionOptions
		{
			ConsulAddress = consulAddress,
		};

		// Assert
		options.ConsulAddress.ShouldBe(consulAddress);
	}

	[Fact]
	public void Have_Default_KeyPrefix()
	{
		// Arrange & Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.KeyPrefix.ShouldBe("excalibur/leader-election");
	}

	[Fact]
	public void Have_Default_SessionTTL()
	{
		// Arrange & Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.SessionTTL.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Have_Default_LockDelay()
	{
		// Arrange & Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.LockDelay.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void Have_Default_MaxRetryAttempts()
	{
		// Arrange & Act
		var options = new ConsulLeaderElectionOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Allow_Setting_Datacenter()
	{
		// Arrange
		const string datacenter = "dc1";

		// Act
		var options = new ConsulLeaderElectionOptions
		{
			Datacenter = datacenter,
		};

		// Assert
		options.Datacenter.ShouldBe(datacenter);
	}

	[Fact]
	public void Allow_Setting_Token()
	{
		// Arrange
		const string token = "my-acl-token";

		// Act
		var options = new ConsulLeaderElectionOptions
		{
			Token = token,
		};

		// Assert
		options.Token.ShouldBe(token);
	}
}
