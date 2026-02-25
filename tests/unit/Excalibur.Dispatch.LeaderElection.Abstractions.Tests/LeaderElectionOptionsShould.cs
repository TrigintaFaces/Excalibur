// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaderElectionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderElectionOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new LeaderElectionOptions();

		// Assert
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(15));
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.RetryInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(5));
		options.EnableHealthChecks.ShouldBeTrue();
		options.MinimumHealthScore.ShouldBe(0.8);
		options.StepDownWhenUnhealthy.ShouldBeTrue();
	}

	[Fact]
	public void InstanceId_IsNotEmpty()
	{
		// Act
		var options = new LeaderElectionOptions();

		// Assert
		options.InstanceId.ShouldNotBeNullOrWhiteSpace();
		options.InstanceId.Length.ShouldBe(24);
	}

	[Fact]
	public void CandidateMetadata_IsInitialized()
	{
		// Act
		var options = new LeaderElectionOptions();

		// Assert
		options.CandidateMetadata.ShouldNotBeNull();
		options.CandidateMetadata.ShouldBeEmpty();
	}

	[Fact]
	public void CandidateMetadata_CanBePopulated()
	{
		// Arrange
		var options = new LeaderElectionOptions();

		// Act
		options.CandidateMetadata["role"] = "primary";

		// Assert
		options.CandidateMetadata.ShouldContainKeyAndValue("role", "primary");
	}

	[Fact]
	public void AllProperties_CanBeSet()
	{
		// Act
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(3),
			InstanceId = "test-instance-001",
			GracePeriod = TimeSpan.FromSeconds(10),
			EnableHealthChecks = false,
			MinimumHealthScore = 0.5,
			StepDownWhenUnhealthy = false,
		};

		// Assert
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.RetryInterval.ShouldBe(TimeSpan.FromSeconds(3));
		options.InstanceId.ShouldBe("test-instance-001");
		options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(10));
		options.EnableHealthChecks.ShouldBeFalse();
		options.MinimumHealthScore.ShouldBe(0.5);
		options.StepDownWhenUnhealthy.ShouldBeFalse();
	}
}
