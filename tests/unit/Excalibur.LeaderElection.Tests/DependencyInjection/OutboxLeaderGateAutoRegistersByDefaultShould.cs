// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.LeaderElection.MongoDB;

using MongoDB.Driver;

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Regression lock for jbig3v: <c>UseMongoDB()</c>, <c>UseRedis()</c>, <c>UseSqlServer()</c> and
/// <c>AddSqlServerHealthBasedLeaderElection()</c> must all wire the outbox leader gate, matching
/// Consul/Kubernetes/InMemory/Postgres (which already did).
/// </summary>
/// <remarks>
/// <para>
/// Before this fix, registering one of these four entry points left <see cref="ILeaderProcessingGate"/>
/// unresolved. h2hdlg's startup invariant refuses host startup in that state rather than draining
/// unfenced (so this was never the split-brain-by-default defect h2hdlg closed) — but a consumer on one
/// of these four providers still hit a startup failure that Consul/Kubernetes/InMemory/Postgres consumers
/// never see, for no principled reason. Fixed when <see cref="ILeaderProcessingGate"/> resolves from the
/// entry point alone, with no explicit <c>WithLeaderElection()</c> call.
/// </para>
/// <para>
/// <c>UseSqlServerFactory()</c> is DELIBERATELY excluded from the positive list and asserted the other
/// way (below): it registers only a KEYED <c>ILeaderElectionFactory</c>, never an unkeyed
/// <see cref="ILeaderElection"/> — there is no single canonical election for a multi-lock factory to
/// expose the gate against. Wiring the gate there would register a factory that can never resolve,
/// silently replacing h2hdlg's clean startup refusal with a raw DI exception at first drain.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class OutboxLeaderGateAutoRegistersByDefaultShould
{
	private const string SqlServerConnectionString = "Server=localhost;Database=excalibur-tests;User Id=test;Password=test;";

	public static TheoryData<string> EntryPoints() =>
	[
		"mongodb-builder",
		"redis-builder",
		"sqlserver-builder",
		"sqlserver-healthbased-standalone",
	];

	[Theory]
	[MemberData(nameof(EntryPoints))]
	public async Task ResolveTheOutboxLeaderGate_WhenRegisteredWithNoExplicitOutboxCall(string entryPoint)
	{
		// Async disposal: the registered ILeaderElection resolves through TelemetryLeaderElection, which
		// is IAsyncDisposable-only — a sync `using` throws on Dispose() once this test resolves it.
		await using var services = Build(entryPoint);

		services.GetService<ILeaderProcessingGate>().ShouldNotBeNull(
			$"'{entryPoint}' must wire the outbox leader gate the same way Consul/Kubernetes/InMemory/" +
			"Postgres already do. If this is null, a consumer on this provider hits h2hdlg's startup " +
			"refusal for registering leader election at all — the exact gap jbig3v closes.");
	}

	[Fact]
	public async Task NotResolveTheOutboxLeaderGate_ForTheFactoryPattern_SoH2hdlgStaysTheRealBackstop()
	{
		// MEASURED, not assumed (this arm is what caught it): UseSqlServerFactory() exposes only a keyed
		// ILeaderElectionFactory. If GetService<ILeaderProcessingGate>() ever returns non-null here, either
		// the factory pattern gained a default unkeyed election (safe to wire the gate) or RegisterOutboxGate
		// was mistakenly added back to RegisterFactoryServices (unsafe — see the class remarks).
		await using var services = Build("sqlserver-factory-builder");

		services.GetService<ILeaderProcessingGate>().ShouldBeNull(
			"UseSqlServerFactory() has no unkeyed ILeaderElection to gate on. A non-null result here means " +
			"the outbox leader gate is registered but can never actually resolve at runtime — worse than " +
			"not registering it, because it silently defeats h2hdlg's clean startup refusal.");
	}

	private static ServiceProvider Build(string entryPoint)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		switch (entryPoint)
		{
			case "mongodb-builder":
				_ = services.AddExcaliburLeaderElection(le =>
					le.UseMongoDB("excalibur-tests-leader", m => m.Client(A.Fake<IMongoClient>()).DatabaseName("excalibur-tests")));
				break;

			case "redis-builder":
				_ = services.AddExcaliburLeaderElection(le =>
					le.UseRedis(r => r.ConnectionMultiplexer(A.Fake<IConnectionMultiplexer>()).LockKey("excalibur-tests")));
				break;

			case "sqlserver-builder":
				_ = services.AddExcaliburLeaderElection(le =>
					le.UseSqlServer(s => s.ConnectionString(SqlServerConnectionString).LockResource("excalibur-tests-leader")));
				break;

			case "sqlserver-factory-builder":
				_ = services.AddExcaliburLeaderElection(le =>
					le.UseSqlServerFactory(s => s.ConnectionString(SqlServerConnectionString)));
				break;

			case "sqlserver-healthbased-standalone":
				_ = services.AddSqlServerHealthBasedLeaderElection(SqlServerConnectionString, "excalibur-tests-leader");
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, "Unknown entry point.");
		}

		return services.BuildServiceProvider(validateScopes: false);
	}
}
