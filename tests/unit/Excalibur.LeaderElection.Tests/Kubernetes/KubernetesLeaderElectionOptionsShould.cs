// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
public class KubernetesLeaderElectionOptionsShould
{
	[Fact]
	public void Inherit_From_LeaderElectionOptions()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		_ = options.ShouldBeAssignableTo<LeaderElectionOptions>();
	}

	[Fact]
	public void Have_Null_Namespace_By_Default()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.Namespace.ShouldBeNull();
	}

	[Fact]
	public void Allow_Setting_Namespace()
	{
		// Arrange
		const string namespace_ = "my-namespace";

		// Act
		var options = new KubernetesLeaderElectionOptions
		{
			Namespace = namespace_,
		};

		// Assert
		options.Namespace.ShouldBe(namespace_);
	}

	[Fact]
	public void Have_Null_LeaseName_By_Default()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.LeaseName.ShouldBeNull();
	}

	[Fact]
	public void Allow_Setting_LeaseName()
	{
		// Arrange
		const string leaseName = "my-lease";

		// Act
		var options = new KubernetesLeaderElectionOptions
		{
			LeaseName = leaseName,
		};

		// Assert
		options.LeaseName.ShouldBe(leaseName);
	}

	[Fact]
	public void Have_Default_LeaseDurationSeconds()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.LeaseDurationSeconds.ShouldBe(15);
	}

	[Fact]
	public void Have_Default_RenewIntervalMilliseconds()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.RenewIntervalMilliseconds.ShouldBe(5000);
	}

	[Fact]
	public void Have_Default_RetryIntervalMilliseconds()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.RetryIntervalMilliseconds.ShouldBe(2000);
	}

	[Fact]
	public void Have_Default_GracePeriodSeconds()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.GracePeriodSeconds.ShouldBe(5);
	}

	[Fact]
	public void Have_Default_MaxRetries()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void Have_Default_MaxRetryDelayMilliseconds()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.MaxRetryDelayMilliseconds.ShouldBe(5000);
	}

	[Fact]
	public void Have_StepDownWhenUnhealthy_True_By_Default()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.StepDownWhenUnhealthy.ShouldBeTrue();
	}

	[Fact]
	public void Have_Empty_CandidateMetadata_By_Default()
	{
		// Arrange & Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		_ = options.CandidateMetadata.ShouldNotBeNull();
		options.CandidateMetadata.ShouldBeEmpty();
	}

	[Fact]
	public void Allow_Adding_CandidateMetadata()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions();

		// Act
		options.CandidateMetadata["key1"] = "value1";
		options.CandidateMetadata["key2"] = "value2";

		// Assert
		options.CandidateMetadata.Count.ShouldBe(2);
		options.CandidateMetadata["key1"].ShouldBe("value1");
		options.CandidateMetadata["key2"].ShouldBe("value2");
	}
}
