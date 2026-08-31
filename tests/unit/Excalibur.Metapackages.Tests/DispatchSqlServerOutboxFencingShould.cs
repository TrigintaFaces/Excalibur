// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.LeaderElection.SqlServer;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Startup lock for the outbox that <c>AddDispatchWithSqlServer</c> registers, under a leader election.
/// </summary>
/// <remarks>
/// <para>
/// Registering an outbox in the one-liner reaches a check that resolving the store alone does not.
/// When a leader gate is present and the host has not opted out of single-active-writer, the outbox
/// refuses to start against a store that cannot enforce a fencing high-water mark, rather than let a
/// superseded leader drain unfenced. Hosts that combine this metapackage with leader election — which the
/// full-stack metapackage does by default — therefore hit that check at boot.
/// </para>
/// <para>
/// The failure mode this guards is not a wrong value but a host that will not start, so it is asserted
/// against the real validators rather than reasoned about from the store's declared interfaces.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class DispatchSqlServerOutboxFencingShould : UnitTestBase
{
	private const string ConnectionString =
		"Server=localhost;Database=ExcaliburMetapackageTests;Trusted_Connection=True;TrustServerCertificate=True";

	private static ServiceProvider BuildProviderWithLeaderElection()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchWithSqlServer(ConnectionString);
		_ = services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
			le.UseSqlServer(sql => sql
				.ConnectionString(ConnectionString)
				.LockResource("excalibur-metapackage-tests"))));
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task ArmTheFencingCheckWhenALeaderElectionIsPresent()
	{
		// Non-vacuity guard for the test below: the startup fencing invariant is skipped entirely when no
		// leader gate is registered, so without this assertion a passing SatisfyStartupPrerequisites would
		// prove only that the check never ran.
		await using var provider = BuildProviderWithLeaderElection();

		provider.GetService<ILeaderProcessingGate>().ShouldNotBeNull();
	}

	[Fact]
	public async Task SatisfyStartupPrerequisitesUnderThatFencingCheck()
	{
		// The store the one-liner registers must be fencing-capable, or every host that pairs this
		// metapackage with a leader election fails to boot. Validators are documented as I/O-free and
		// safe to call repeatedly, so this runs the real check with no database present.
		await using var provider = BuildProviderWithLeaderElection();

		// Non-vacuity, in the direction that actually bites: the fencing check only judges a store that
		// exists, and other subsystems register validators of their own -- so "some validator passed" is
		// not evidence. Confirm the outbox store is present first, then run the checks over it.
		provider.GetService<IOutboxStore>().ShouldNotBeNull(
			"with no outbox store registered the fencing check has nothing to judge and this test is vacuous.");

		var validators = provider.GetServices<IStartupPrerequisiteValidator>().ToList();

		validators.ShouldNotBeEmpty(
			"the outbox registers a startup validator; with none present this test proves nothing.");
		Should.NotThrow(() =>
		{
			foreach (var validator in validators)
			{
				validator.Validate();
			}
		});
	}
}
