// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Tests for DI registration behavior for Excalibur leader election services.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderElectionServiceCollectionExtensionsShould
{
	[Fact]
	public void AddExcaliburLeaderElection_WithNoConfiguration_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<LeaderElectionOptions>>();
		_ = options.ShouldNotBeNull();
	}

	[Fact]
	public void AddExcaliburLeaderElection_WithNoConfiguration_AppliesDefaultValues()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection();

		// Assert — accessing .Value forces the empty configure lambda (_ => { }) to execute
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(15));
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.RetryInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(5));
		options.EnableHealthChecks.ShouldBeTrue();
		options.MinimumHealthScore.ShouldBe(0.8);
		options.StepDownWhenUnhealthy.ShouldBeTrue();
		options.InstanceId.ShouldNotBeNullOrWhiteSpace();
		options.CandidateMetadata.ShouldNotBeNull();
		options.CandidateMetadata.ShouldBeEmpty();
	}

	[Fact]
	public void AddExcaliburLeaderElection_WithConfiguration_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		const string expectedInstanceId = "test-instance";
		var expectedLeaseDuration = TimeSpan.FromMinutes(5);

		// Act
		_ = services.AddExcaliburLeaderElection(options =>
		{
			options.InstanceId = expectedInstanceId;
			options.LeaseDuration = expectedLeaseDuration;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.InstanceId.ShouldBe(expectedInstanceId);
		options.LeaseDuration.ShouldBe(expectedLeaseDuration);
	}

	[Fact]
	public void AddExcaliburLeaderElection_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburLeaderElection(_ => { }));
	}

	[Fact]
	public void AddExcaliburLeaderElection_DefaultOverload_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburLeaderElection());
	}

	[Fact]
	public void AddExcaliburLeaderElection_WithNullConfigure_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburLeaderElection(null!));
	}

	[Fact]
	public void AddExcaliburLeaderElection_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburLeaderElection();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburLeaderElection_WithConfiguration_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburLeaderElection(options => options.InstanceId = "test");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburLeaderElection_ConfiguresHealthCheckOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection(options =>
		{
			options.EnableHealthChecks = false;
			options.MinimumHealthScore = 0.5;
			options.StepDownWhenUnhealthy = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.EnableHealthChecks.ShouldBeFalse();
		options.MinimumHealthScore.ShouldBe(0.5);
		options.StepDownWhenUnhealthy.ShouldBeFalse();
	}

	[Fact]
	public void AddExcaliburLeaderElection_CalledMultipleTimes_LastConfigurationWins()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection(options => options.InstanceId = "first");
		_ = services.AddExcaliburLeaderElection(options => options.InstanceId = "second");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.InstanceId.ShouldBe("second");
	}

	[Fact]
	public void AddExcaliburLeaderElection_ConfiguresCandidateMetadata()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection(options =>
		{
			options.CandidateMetadata["region"] = "us-east-1";
			options.CandidateMetadata["version"] = "1.0.0";
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.CandidateMetadata.Count.ShouldBe(2);
		options.CandidateMetadata["region"].ShouldBe("us-east-1");
		options.CandidateMetadata["version"].ShouldBe("1.0.0");
	}
}
