// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Behavioral regression lock: the Redis and SQL Server leader-election factories must propagate
/// consumer-configured <see cref="LeaderElectionOptions"/> timing through to every election they
/// create, rather than constructing fresh default options.
/// </summary>
/// <remarks>
/// Before the factories were changed to inject <c>IOptions&lt;LeaderElectionOptions&gt;</c> and
/// resolve via the shared <c>ResolveOptions</c> helper, they built fresh default options — silently
/// discarding configured <c>LeaseDuration</c>/<c>RenewInterval</c>. These tests fail (RED) against
/// that pre-fix behavior because the created election would carry the 15s/5s defaults instead of the
/// configured values asserted below.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderElectionFactoryConfiguredTimingShould : UnitTestBase
{
	private static readonly TimeSpan ConfiguredLease = TimeSpan.FromSeconds(42);
	private static readonly TimeSpan ConfiguredRenew = TimeSpan.FromSeconds(13);

	private static LeaderElectionOptions ResolvedOptionsOf(ILeaderElection election, string fieldName)
	{
		var field = election.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
		field.ShouldNotBeNull($"{election.GetType().Name} must store its resolved options in '{fieldName}'");
		return (LeaderElectionOptions)field!.GetValue(election)!;
	}

	[Fact]
	public void Redis_factory_propagates_configured_timing_to_created_election()
	{
		// Arrange — non-default timing (defaults are 15s lease / 5s renew).
		var configured = new LeaderElectionOptions { LeaseDuration = ConfiguredLease, RenewInterval = ConfiguredRenew };
		var factory = new RedisLeaderElectionFactory(
			A.Fake<IConnectionMultiplexer>(), new LoggerFactory(), Options.Create(configured));

		// Act
		var election = factory.CreateElection("test:leader", candidateId: null);

		// Assert
		var resolved = ResolvedOptionsOf(election, "_options");
		resolved.LeaseDuration.ShouldBe(ConfiguredLease);
		resolved.RenewInterval.ShouldBe(ConfiguredRenew);
	}

	[Fact]
	public void SqlServer_factory_propagates_configured_timing_to_created_election()
	{
		// Arrange
		var configured = new LeaderElectionOptions { LeaseDuration = ConfiguredLease, RenewInterval = ConfiguredRenew };
		var factory = new SqlServerLeaderElectionFactory(
			"Server=localhost;Database=test;Trusted_Connection=true;", new LoggerFactory(), Options.Create(configured));

		// Act
		var election = factory.CreateElection("test-leader", candidateId: null);

		// Assert
		var resolved = ResolvedOptionsOf(election, "_options");
		resolved.LeaseDuration.ShouldBe(ConfiguredLease);
		resolved.RenewInterval.ShouldBe(ConfiguredRenew);
	}

	[Fact]
	public void Redis_factory_rejects_null_election_options()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new RedisLeaderElectionFactory(A.Fake<IConnectionMultiplexer>(), new LoggerFactory(), options: null!));
	}

	[Fact]
	public void SqlServer_factory_rejects_null_election_options()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerLeaderElectionFactory("Server=localhost;Database=test;", new LoggerFactory(), options: null!));
	}
}
