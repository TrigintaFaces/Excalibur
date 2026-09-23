// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;
using Excalibur.Cdc.SqlServer;
using Excalibur.Dispatch.LeaderElection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// A leader election registered by the application must reach the HOSTED CDC processor, not only the
/// direct one.
/// </summary>
/// <remarks>
/// <para>
/// The builder registers two processors from one configuration. The direct processor resolved
/// <c>ILeaderElection</c>; the hosted <c>DataChangeEventProcessor</c> behind
/// <c>ICdcBackgroundProcessor</c> did not, so an application that configured single-active consumption
/// got a hosted follower on the null-election unconditional path, checkpointing with a null token —
/// multiple replicas processing the same feed with no fence between them.
/// </para>
/// <para>
/// These arms resolve through the real container rather than asserting that a service type appears in
/// the collection. Registration presence is what the sibling suite checks, and it is satisfied by the
/// defective wiring: the hosted processor WAS registered, just without its coordination.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class HostedCdcProcessorReceivesLeaderElectionShould : UnitTestBase
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";

	[Fact]
	public void HandTheRegisteredElectionToTheHostedProcessor()
	{
		// SAFETY. The dependency must actually arrive on the instance the host will run.
		var election = A.Fake<ILeaderElection>();
		using var provider = BuildProvider(election);

		var hosted = provider.GetRequiredService<IDataChangeEventProcessor>();

		LeaderElectionOf(hosted).ShouldBeSameAs(election,
			"the hosted CDC processor did not receive the registered ILeaderElection, so it takes the "
			+ "null-election unconditional path and checkpoints unfenced — replicas would process the "
			+ "same feed concurrently despite the application configuring single-active consumption");
	}

	[Fact]
	public void StillResolveWhenNoElectionIsRegistered()
	{
		// LIVENESS. Single-instance hosting registers no election and must remain supported. Without this
		// arm, wiring that refused to resolve without one would satisfy the assertion above while breaking
		// every single-instance deployment.
		using var provider = BuildProvider(election: null);

		var hosted = provider.GetRequiredService<IDataChangeEventProcessor>();

		hosted.ShouldNotBeNull();
		LeaderElectionOf(hosted).ShouldBeNull(
			"with no election registered the hosted processor must run unconditionally, as it always has");
	}

	private static ServiceProvider BuildProvider(ILeaderElection? election)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IHostApplicationLifetime>());
		_ = services.AddSingleton(A.Fake<IDatabaseOptions>());

		if (election is not null)
		{
			_ = services.AddSingleton(election);
		}

		_ = services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .SchemaName("cdc")
				   .StateTableName("State")));

		return services.BuildServiceProvider();
	}

	// The field is private on the base processor. Reading it is the narrowest way to assert the
	// dependency ARRIVED; the alternative is driving a real multi-host SQL Server, which the bead's own
	// acceptance asks for separately and which this arm does not claim to replace.
	private static ILeaderElection? LeaderElectionOf(IDataChangeEventProcessor processor)
	{
		var field = processor.GetType()
			.GetField("_leaderElection", BindingFlags.Instance | BindingFlags.NonPublic)
			?? processor.GetType().BaseType?
				.GetField("_leaderElection", BindingFlags.Instance | BindingFlags.NonPublic);

		field.ShouldNotBeNull("the field this arm reads was renamed; the arm is measuring nothing");
		return (ILeaderElection?)field!.GetValue(processor);
	}
}
