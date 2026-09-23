// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.MongoDB;
using Excalibur.LeaderElection.Postgres;

using MongoDB.Driver;

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Regression lock for b8ht6u: every leader-election registration path a consumer can reach must
/// auto-register that store's arbitrated <see cref="IFencingTokenProvider"/> by default, and
/// <c>WithoutFencingTokens()</c> must be able to suppress it on every one of those same paths.
/// </summary>
/// <remarks>
/// <para>
/// THE BUG, pre-b8ht6u: fencing was opt-in, split across two calls (<c>Add{Store}LeaderElection()</c> for
/// the election, then a separate <c>Add{Store}FencingTokenProvider()</c> + <c>WithFencingTokens()</c> for
/// the fence). A consumer who registered only the election got an election with NO fencing, and nothing
/// told them — the failure mode is silent data corruption from a stalled ex-leader's writes landing after
/// a new leader is elected, not an exception anyone would see.
/// </para>
/// <para>
/// SAFETY arm: build a real <see cref="ServiceProvider"/> from the production entry point with NO explicit
/// fencing call, and assert <see cref="IFencingTokenProvider"/> resolves to the store's own arbitrated
/// provider type (checked by name rather than a direct cast, since every concrete provider type is
/// <see langword="internal"/> and several of these projects do not grant this test project
/// <c>InternalsVisibleTo</c>).
/// </para>
/// <para>
/// OPT-OUT arm: the same entry point, with <c>WithoutFencingTokens()</c> also called, must leave
/// <see cref="IFencingTokenProvider"/> unresolved (<see langword="null"/> via <c>GetService</c>) — proving
/// the switch actually suppresses registration rather than merely disabling a stub, and proving the arm is
/// non-vacuous in the other direction from the safety arm above.
/// </para>
/// <para>
/// Every public <c>Add*LeaderElection*</c> / <c>Use*</c> entry point across all seven provider packages is
/// covered here, including entry points that live in a second, parallel DI-registration method within the
/// same package (e.g. Consul's standalone <c>AddConsulLeaderElection</c> vs. its builder-path
/// <c>UseConsul</c> — two independent <c>RegisterOptionsAndServices</c> implementations that do not share
/// code and so do not share a bug fix).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class FencingAutoRegistersByDefaultShould
{
	private const string ConsulAddress = "http://localhost:8500";
	private const string PostgresConnectionString = "Host=localhost;Database=excalibur-tests;Username=test;Password=test";
	private const string SqlServerConnectionString = "Server=localhost;Database=excalibur-tests;User Id=test;Password=test;";

	public static TheoryData<string, string> EntryPoints() => new()
	{
		{ "consul-standalone", "ConsulFencingTokenProvider" },
		{ "consul-builder", "ConsulFencingTokenProvider" },
		{ "kubernetes-standalone", "KubernetesFencingTokenProvider" },
		{ "kubernetes-builder", "KubernetesFencingTokenProvider" },
		{ "inmemory-standalone", "InMemoryFencingTokenProvider" },
		{ "inmemory-builder", "InMemoryFencingTokenProvider" },
		{ "mongodb-builder", "MongoDbFencingTokenProvider" },
		{ "postgres-standalone", "PostgresFencingTokenProvider" },
		{ "postgres-healthbased-standalone", "PostgresFencingTokenProvider" },
		{ "postgres-builder", "PostgresFencingTokenProvider" },
		{ "redis-builder", "RedisFencingTokenProvider" },
		{ "sqlserver-healthbased-standalone", "SqlServerFencingTokenProvider" },
		{ "sqlserver-builder", "SqlServerFencingTokenProvider" },
		{ "sqlserver-factory-builder", "SqlServerFencingTokenProvider" },
	};

	[Theory]
	[MemberData(nameof(EntryPoints))]
	public void ResolveTheStoresArbitratedProvider_WhenRegisteredWithNoExplicitFencingCall(string entryPoint, string expectedProviderTypeName)
	{
		// SAFETY. No AddXFencingTokenProvider() / WithFencingTokens() call anywhere in Build() below — the
		// entry point alone must be enough.
		using var services = Build(entryPoint, optOut: false);

		var provider = services.GetRequiredService<IFencingTokenProvider>();

		provider.GetType().Name.ShouldBe(expectedProviderTypeName,
			$"'{entryPoint}' must auto-register its OWN arbitrated fencing-token provider, not a null/no-op " +
			"stand-in and not another store's provider left over from a prior registration in the same test.");
	}

	[Theory]
	[MemberData(nameof(EntryPoints))]
	public void LeaveTheProviderUnresolved_WhenWithoutFencingTokensIsCalled(string entryPoint, string _)
	{
		// OPT-OUT arm, proven in the direction the safety arm cannot: the switch must actually suppress
		// registration, not merely swap in a disabled stub that still satisfies GetRequiredService.
		using var services = Build(entryPoint, optOut: true);

		services.GetService<IFencingTokenProvider>().ShouldBeNull(
			$"WithoutFencingTokens() on '{entryPoint}' must leave IFencingTokenProvider unresolved, proving " +
			"the opt-out suppresses registration rather than disabling a stub that would still resolve.");
	}

	private static ServiceProvider Build(string entryPoint, bool optOut)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		switch (entryPoint)
		{
			case "consul-standalone":
				_ = services.AddConsulLeaderElection(c => c.Address(ConsulAddress).LockKey("excalibur-tests").ResourceName("excalibur-tests-leader"));
				if (optOut)
				{
					_ = services.WithoutFencingTokens();
				}

				break;

			case "consul-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseConsul(c => c.Address(ConsulAddress).LockKey("excalibur-tests"));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			case "kubernetes-standalone":
				_ = services.AddExcaliburKubernetesLeaderElection(k => k.LeaseName("excalibur-tests-leader"));
				if (optOut)
				{
					_ = services.WithoutFencingTokens();
				}

				// Registered AFTER the provider so it wins resolution (AddSingleton<IKubernetes> is not
				// TryAdd, and DI resolves the LAST registration for a non-keyed single-instance request).
				// Building a real client needs in-cluster config or a kubeconfig file, neither of which
				// exists on a build agent; this arm asserts DI wiring, not Kubernetes behaviour.
				_ = services.AddSingleton(A.Fake<IKubernetes>());
				break;

			case "kubernetes-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseKubernetes(k => k.LeaseName("excalibur-tests-leader"));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});

				// Registered AFTER the provider so it wins resolution — see the standalone case above.
				_ = services.AddSingleton(A.Fake<IKubernetes>());
				break;

			case "inmemory-standalone":
				_ = services.AddInMemoryLeaderElection();
				if (optOut)
				{
					_ = services.WithoutFencingTokens();
				}

				break;

			case "inmemory-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseInMemory();
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			case "mongodb-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseMongoDB("excalibur-tests-leader", m => m.Client(A.Fake<IMongoClient>()).DatabaseName("excalibur-tests"));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			case "postgres-standalone":
				_ = services.AddPostgresLeaderElection(o => o.ConnectionString = PostgresConnectionString);
				if (optOut)
				{
					_ = services.WithoutFencingTokens();
				}

				break;

			case "postgres-healthbased-standalone":
				_ = services.AddPostgresHealthBasedLeaderElection(o => o.ConnectionString = PostgresConnectionString);
				if (optOut)
				{
					_ = services.WithoutFencingTokens();
				}

				break;

			case "postgres-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UsePostgres(p => p.ConnectionString(PostgresConnectionString).LockKey(1));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			case "redis-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseRedis(r => r.ConnectionMultiplexer(A.Fake<IConnectionMultiplexer>()).LockKey("excalibur-tests"));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			case "sqlserver-healthbased-standalone":
				_ = services.AddSqlServerHealthBasedLeaderElection(SqlServerConnectionString, "excalibur-tests-leader");
				if (optOut)
				{
					_ = services.WithoutFencingTokens();
				}

				break;

			case "sqlserver-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseSqlServer(s => s.ConnectionString(SqlServerConnectionString).LockResource("excalibur-tests-leader"));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			case "sqlserver-factory-builder":
				_ = services.AddExcaliburLeaderElection(le =>
				{
					_ = le.UseSqlServerFactory(s => s.ConnectionString(SqlServerConnectionString));
					if (optOut)
					{
						_ = le.WithoutFencingTokens();
					}
				});
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, "Unknown entry point.");
		}

		return services.BuildServiceProvider(validateScopes: false);
	}
}
