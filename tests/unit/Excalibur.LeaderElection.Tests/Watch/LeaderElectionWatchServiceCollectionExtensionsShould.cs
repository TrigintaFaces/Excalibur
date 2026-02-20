// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Watch;

namespace Excalibur.LeaderElection.Tests.Watch;

/// <summary>
/// Unit tests for <see cref="LeaderElectionWatchServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class LeaderElectionWatchServiceCollectionExtensionsShould
{
	// --- AddLeaderElectionWatcher(configure) ---

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElectionWatcher(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElectionWatcher(null!));
	}

	[Fact]
	public void RegisterDefaultLeaderElectionWatcher()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionWatcher(options =>
		{
			options.PollInterval = TimeSpan.FromSeconds(10);
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ILeaderElectionWatcher) &&
			sd.ImplementationType == typeof(DefaultLeaderElectionWatcher));
	}

	[Fact]
	public void RegisterOptionsWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionWatcher(options =>
		{
			options.PollInterval = TimeSpan.FromSeconds(15);
			options.IncludeHeartbeats = true;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderWatchOptions>>().Value;
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(15));
		options.IncludeHeartbeats.ShouldBeTrue();
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddLeaderElectionWatcher(_ => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void NotDuplicateWatcherRegistrationOnMultipleCalls()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — register twice
		services.AddLeaderElectionWatcher(_ => { });
		services.AddLeaderElectionWatcher(_ => { });

		// Assert — TryAddSingleton should prevent duplicates
		var watcherRegistrations = services
			.Where(sd => sd.ServiceType == typeof(ILeaderElectionWatcher))
			.ToList();
		watcherRegistrations.Count.ShouldBe(1);
	}

	// --- AddLeaderElectionWatcher() default overload ---

	[Fact]
	public void DefaultOverloadThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddLeaderElectionWatcher());
	}

	[Fact]
	public void DefaultOverloadRegisterWithDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddLeaderElectionWatcher();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ILeaderElectionWatcher) &&
			sd.ImplementationType == typeof(DefaultLeaderElectionWatcher));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderWatchOptions>>().Value;
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.IncludeHeartbeats.ShouldBeFalse();
	}

	[Fact]
	public void DefaultOverloadReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddLeaderElectionWatcher();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
