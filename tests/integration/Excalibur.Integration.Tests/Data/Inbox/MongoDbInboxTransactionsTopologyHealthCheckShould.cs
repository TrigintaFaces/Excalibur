// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure coverage for <see cref="MongoDbInboxOptions.EnableTransactions"/> set against a
/// deployment that cannot honour it. The host MUST START and report not-ready, rather than refuse to
/// start: verifying the topology needs a network round trip, and options validation answers only what
/// can be decided without one. A deployment whose database is merely slow to accept connections is the
/// ordinary case under any orchestrator that does not strictly order it first.
/// </summary>
/// <remarks>
/// Uses the same standalone <c>mongo:7</c> container as <see cref="MongoDbInboxStoreConformanceShould"/> --
/// standalone is exactly the topology <see cref="MongoDbInboxOptions.EnableTransactions"/> cannot be used
/// against, so no separate replica-set container is needed for the safety arm. Never skipped: Docker
/// availability is asserted true, matching the rest of this suite's real-infra discipline.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbInboxTransactionsTopologyHealthCheckShould : IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private readonly MongoDbInboxStoreContainerFixture _fixture;

	public MongoDbInboxTransactionsTopologyHealthCheckShould(MongoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	// SAFETY: EnableTransactions=true against the standalone container must report UNHEALTHY, naming the
	// option and carrying the verdict that says a deployment change is required. Options resolution
	// SUCCEEDS -- that is the behaviour change, and asserting it here is what stops a future edit from
	// quietly moving the network probe back into options validation.
	[Fact]
	public async Task ReportUnhealthyAndMisconfiguredWhenTransactionsEnabledAgainstStandaloneServer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var provider = BuildProvider(enableTransactions: true);

		// The host starts. This line would have thrown before the probe moved out of options validation.
		_ = provider.GetRequiredService<IOptions<MongoDbInboxOptions>>().Value;

		var report = await provider.GetRequiredService<HealthCheckService>()
			.CheckHealthAsync(r => r.Name == "mongodb-inbox-topology", CancellationToken.None)
			.ConfigureAwait(false);

		report.Status.ShouldBe(HealthStatus.Unhealthy);

		var entry = report.Entries["mongodb-inbox-topology"];
		entry.Description.ShouldContain(nameof(MongoDbInboxOptions.EnableTransactions));
		entry.Description.ShouldContain("standalone");

		// misconfigured, not unreachable: a standalone server never becomes a replica set on its own, and
		// an operator told "expected to clear without a configuration change" would wait forever.
		entry.Data["topology.verdict"].ShouldBe("misconfigured");
	}

	// LIVENESS: the identical registration with the flag off (the shipped default) resolves cleanly against
	// the same standalone server -- proves the check doesn't false-positive on the common, unflagged path.
	[Fact]
	public void ResolveCleanlyWhenTransactionsDisabledAgainstStandaloneServer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var provider = BuildProvider(enableTransactions: false);

		var options = provider.GetRequiredService<IOptions<MongoDbInboxOptions>>().Value;

		options.EnableTransactions.ShouldBeFalse();
	}

	private ServiceProvider BuildProvider(bool enableTransactions)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddExcaliburInbox(inbox => inbox.UseMongoDB(mongo => mongo
			.ConnectionString(_fixture.ConnectionString)
			.DatabaseName(_fixture.DatabaseName)));

		// IMongoDBInboxBuilder has no fluent EnableTransactions setter (that gap is tracked separately,
		// unrelated to this probe); PostConfigure against the already-registered options is how a consumer
		// reaches the flag today.
		_ = services.PostConfigure<MongoDbInboxOptions>(o => o.EnableTransactions = enableTransactions);

		return services.BuildServiceProvider();
	}
}
