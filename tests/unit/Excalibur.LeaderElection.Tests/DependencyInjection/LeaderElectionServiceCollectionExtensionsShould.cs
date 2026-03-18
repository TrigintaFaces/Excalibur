// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the pre-built options overload of AddExcaliburLeaderElection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LeaderElectionServiceCollectionExtensionsShould : UnitTestBase
{
	// --- Null guards ---

	[Fact]
	public void ThrowOnNullServices_OptionsOverload()
	{
		var options = new LeaderElectionOptions();

		Should.Throw<ArgumentNullException>(() =>
			LeaderElectionServiceCollectionExtensions.AddExcaliburLeaderElection(
				null!, options));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburLeaderElection((LeaderElectionOptions)null!));
	}

	// --- Registration ---

	[Fact]
	public void RegisterOptionsViaIOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new LeaderElectionOptions
		{
			LeaseDuration = TimeSpan.FromSeconds(60),
			RenewInterval = TimeSpan.FromSeconds(20)
		};

		// Act
		services.AddExcaliburLeaderElection(options);

		// Assert
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IOptions<LeaderElectionOptions>>();
		resolved.Value.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(60));
		resolved.Value.RenewInterval.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void FirstOptionsWinsViaTryAddSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var first = new LeaderElectionOptions { LeaseDuration = TimeSpan.FromSeconds(30) };
		var second = new LeaderElectionOptions { LeaseDuration = TimeSpan.FromSeconds(999) };

		// Act
		services.AddExcaliburLeaderElection(first);
		services.AddExcaliburLeaderElection(second);

		// Assert - TryAddSingleton means first wins
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IOptions<LeaderElectionOptions>>();
		resolved.Value.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	// --- Fluent chaining ---

	[Fact]
	public void ReturnSameServiceCollection()
	{
		var services = new ServiceCollection();
		var options = new LeaderElectionOptions();

		var result = services.AddExcaliburLeaderElection(options);

		result.ShouldBeSameAs(services);
	}
}
