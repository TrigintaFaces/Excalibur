// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// Extended unit tests for <see cref="KubernetesLeaderElectionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class KubernetesLeaderElectionOptionsExtendedShould
{
	[Fact]
	public void HaveDefaultLeaseDurationOf15Seconds()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void HaveDefaultRenewIntervalOf5000Ms()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.RenewInterval.ShouldBe(TimeSpan.FromMilliseconds(5000));
	}

	[Fact]
	public void HaveDefaultRetryIntervalOf2000Ms()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.RetryInterval.ShouldBe(TimeSpan.FromMilliseconds(2000));
	}

	[Fact]
	public void HaveDefaultGracePeriodOf5Seconds()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveDefaultMaxRetriesOf3()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultMaxRetryDelayOf5000Ms()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(5000));
	}

	[Fact]
	public void HaveStepDownWhenUnhealthyTrueByDefault()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.StepDownWhenUnhealthy.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyCandidateMetadataByDefault()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.CandidateMetadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingNamespace()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions { Namespace = "my-namespace" };

		// Assert
		options.Namespace.ShouldBe("my-namespace");
	}

	[Fact]
	public void AllowSettingLeaseName()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions { LeaseName = "my-lease" };

		// Assert
		options.LeaseName.ShouldBe("my-lease");
	}

	[Fact]
	public void AllowSettingTheInheritedInstanceId()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions { InstanceId = "pod-123" };

		// Assert
		options.InstanceId.ShouldBe(
			"pod-123",
			"this provider used to declare its own CandidateId option and read only that, so a consumer "
			+ "who configured the abstraction's InstanceId got an identity nobody chose. There is now one "
			+ "property for one concept.");
	}

	[Fact]
	public void HaveNullNamespaceByDefault()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.Namespace.ShouldBeNull();
	}

	[Fact]
	public void HaveNullLeaseNameByDefault()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.LeaseName.ShouldBeNull();
	}

	[Fact]
	public void HaveNullInstanceIdByDefault()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions();

		// Assert
		options.InstanceId.ShouldBeNull(
			"null is what makes \"nobody configured one\" EXPRESSIBLE. A non-null default would make a "
			+ "generated identity indistinguishable from a chosen one, and every provider would have to "
			+ "treat the value as deliberate. The identity is resolved from this value exactly once, in "
			+ "LeaderCandidateIdentity.");
	}

	[Fact]
	public void AllowAddingCandidateMetadata()
	{
		// Arrange
		var options = new KubernetesLeaderElectionOptions();

		// Act
		options.CandidateMetadata["region"] = "us-east-1";

		// Assert
		options.CandidateMetadata.Count.ShouldBe(1);
		options.CandidateMetadata["region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void AllowCustomLeaseDuration()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions { LeaseDuration = TimeSpan.FromSeconds(30) };

		// Assert
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowCustomRenewInterval()
	{
		// Act
		var options = new KubernetesLeaderElectionOptions { RenewInterval = TimeSpan.FromMilliseconds(10000) };

		// Assert
		options.RenewInterval.ShouldBe(TimeSpan.FromMilliseconds(10000));
	}
}
