// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.LeaderElection;

using Shouldly;

using Xunit;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Tests to verify that type forwarders are correctly set up in the Excalibur.LeaderElection assembly.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TypeForwardersShould
{
	private static Assembly GetExcaliburLeaderElectionAssembly()
	{
		// Load the assembly directly by name
		return Assembly.Load("Excalibur.LeaderElection");
	}

	[Theory]
	[InlineData(typeof(ILeaderElection))]
	[InlineData(typeof(ILeaderElectionFactory))]
	[InlineData(typeof(IHealthBasedLeaderElection))]
	[InlineData(typeof(LeaderElectionOptions))]
	[InlineData(typeof(LeaderElectionEventArgs))]
	[InlineData(typeof(LeaderChangedEventArgs))]
	[InlineData(typeof(CandidateHealth))]
	public void ForwardExpectedTypes(Type expectedType)
	{
		// Arrange
		var assembly = GetExcaliburLeaderElectionAssembly();
		var forwardedTypes = assembly.GetForwardedTypes();

		// Assert
		forwardedTypes.ShouldContain(expectedType,
			$"Type {expectedType.FullName} should be forwarded from Excalibur.LeaderElection assembly");
	}

	[Fact]
	public void ILeaderElection_BeAccessibleFromDispatchNamespace()
	{
		// Arrange & Act
		var interfaceType = typeof(ILeaderElection);

		// Assert - Verify the type is accessible (this would fail at compile time if not forwarded)
		_ = interfaceType.ShouldNotBeNull();
		interfaceType.IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void LeaderElectionOptions_BeAccessible()
	{
		// Arrange & Act
		var optionsType = typeof(LeaderElectionOptions);

		// Assert
		_ = optionsType.ShouldNotBeNull();
		optionsType.IsClass.ShouldBeTrue();
	}

	[Fact]
	public void LeaderElectionOptions_HaveExpectedProperties()
	{
		// Arrange
		var options = new LeaderElectionOptions();

		// Assert - Verify the default property values
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(15));
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.RetryInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(5));
		options.EnableHealthChecks.ShouldBeTrue();
		options.MinimumHealthScore.ShouldBe(0.8);
		options.StepDownWhenUnhealthy.ShouldBeTrue();
	}

	[Fact]
	public void CandidateHealth_BeAccessibleClassFromDispatchNamespace()
	{
		// Arrange & Act
		var healthType = typeof(CandidateHealth);

		// Assert
		_ = healthType.ShouldNotBeNull();
		healthType.IsClass.ShouldBeTrue();
	}

	[Fact]
	public void CandidateHealth_HaveExpectedProperties()
	{
		// Arrange & Act
		var health = new CandidateHealth
		{
			CandidateId = "test-candidate",
			IsHealthy = true,
			HealthScore = 0.95,
			LastUpdated = DateTimeOffset.UtcNow,
			IsLeader = false
		};

		// Assert
		health.CandidateId.ShouldBe("test-candidate");
		health.IsHealthy.ShouldBeTrue();
		health.HealthScore.ShouldBe(0.95);
		health.IsLeader.ShouldBeFalse();
		_ = health.Metadata.ShouldNotBeNull();
	}
}
