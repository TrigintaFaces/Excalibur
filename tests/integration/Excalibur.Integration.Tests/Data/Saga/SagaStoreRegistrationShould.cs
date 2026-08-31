// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Postgres.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Asserts that each shipped saga provider's PUBLIC registration path produces a resolvable
/// <see cref="ISagaStore"/> that is actually that provider's store.
/// </summary>
/// <remarks>
/// <para>
/// No container is needed: this binds registration, not behaviour. A provider whose registration does not
/// wire its store fails here in milliseconds, and it fails for the reason a consumer would experience —
/// either the contract does not resolve at all, or it resolves to a different store than the one the
/// consumer asked for.
/// </para>
/// <para>
/// The second case is why this asserts the concrete type rather than merely that resolution succeeds. A
/// registration that silently leaves the in-memory default in place resolves fine, starts fine, and loses
/// every saga on restart. "It resolved" is exactly the evidence that cannot distinguish the two.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "STORE")]
public sealed class SagaStoreRegistrationShould
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1823:Avoid unused private fields",
		Justification = "Every test in this class resolves the Postgres store, so the SQL Server provider entry point and composed host path are asserted by none of them. Held until the SQL Server arm exists.")]
	private const string SqlServerConnection =
		"Server=localhost,1433;Database=probe;User Id=sa;Password=Probe_Passw0rd!;TrustServerCertificate=true";

	private const string PostgresConnection =
		"Host=localhost;Port=5432;Database=saga_registration_probe;Username=postgres;Password=postgres";

	/// <summary>
	/// The provider-specific entry point wires the contract a consumer injects.
	/// </summary>
	[Fact]
	public void ResolveThePostgresStore_FromTheProviderEntryPoint()
	{
		var services = new ServiceCollection();

		// A bare ServiceCollection is not a host: a real consumer gets ILogger<T> from
		// Host.CreateApplicationBuilder/WebApplication.CreateBuilder, which every provider's store
		// constructor depends on. Logging is the host's concern, so the fixture supplies it here rather
		// than saga registration taking it over.
		_ = services.AddLogging();

		_ = services.AddPostgresSagaStore(options =>
		{
			options.ConnectionString = PostgresConnection;
			options.Schema = "dispatch";
			options.TableName = "sagas";
		});

		using var provider = services.BuildServiceProvider();

		var store = provider.GetService<ISagaStore>();

		store.ShouldNotBeNull(
			"services.AddPostgresSagaStore(...) is a public entry point, so a consumer who calls it and then "
			+ "injects ISagaStore must receive a store. Resolving null means the package registers its store "
			+ "under a key and never under the contract, which fails at the consumer's first injection.");
		_ = store.ShouldBeOfType<Excalibur.Saga.Postgres.PostgresSagaStore>();
	}

	/// <summary>
	/// The composed host path wires the provider's store, not the in-memory default.
	/// </summary>
	/// <remarks>
	/// This is the shape the kit's own documentation uses. Core saga registration deliberately binds no
	/// store — a saga store is a required deployment decision, not a silent in-memory default — so the
	/// builder callback is the only thing that can supply one. Measured on the descriptors this path
	/// produces: it contains no ISagaStore under any key, because UsePostgres registers options, a
	/// validator and a connection factory and no store at all.
	/// </remarks>
	[Fact]
	public void ResolveThePostgresStore_FromTheComposedHostPath()
	{
		var services = new ServiceCollection();

		// A bare ServiceCollection is not a host — see the note in the provider-entry-point arm.
		_ = services.AddLogging();

		_ = services.AddExcalibur(x => x.AddSagas((ISagaBuilder saga) => saga.UsePostgres(pg =>
			pg.ConnectionString(PostgresConnection)
			  .SchemaName("dispatch")
			  .TableName("sagas"))));

		using var provider = services.BuildServiceProvider();

		var store = provider.GetService<ISagaStore>();

		store.ShouldNotBeNull(
			"UsePostgres was requested, so this path must produce a saga store. Resolving null means the "
			+ "builder extension wired options and a connection factory but never a store, which leaves the "
			+ "consumer's configured provider entirely absent from the container.");
		_ = store.ShouldBeOfType<Excalibur.Saga.Postgres.PostgresSagaStore>(
			"UsePostgres was requested, so ISagaStore must be the Postgres store and not some other "
			+ "provider's store that happened to claim the contract first.");
	}
	/// <summary>
	/// The coordinator constructs on a persistent-only composition, with no in-memory opt-in anywhere.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>SagaCoordinator</c> injects <c>ISagaStore</c> non-keyed while every persistent provider registers its
	/// store keyed. The arms above prove the contract resolves; this one proves the consumer of that contract
	/// does, which is the thing a host actually fails on. A container that resolves the store but cannot build
	/// the coordinator would leave a consumer able to register a production backend and unable to run a saga
	/// with it unless they also called the dev-only in-memory opt-in.
	/// </para>
	/// <para>
	/// The composition deliberately omits <c>AddInMemorySagaStore</c>. Including it would supply the non-keyed
	/// alias by itself and the arm would pass whether or not the persistent path provides one.
	/// </para>
	/// </remarks>
	[Fact]
	public void ResolveTheCoordinator_WithNoInMemoryOptIn()
	{
		var services = new ServiceCollection();

		// A bare ServiceCollection is not a host - see the note in the provider-entry-point arm.
		_ = services.AddLogging();

		_ = services.AddPostgresSagaStore(options =>
		{
			options.ConnectionString = PostgresConnection;
			options.Schema = "dispatch";
			options.TableName = "sagas";
		});
		_ = services.AddExcaliburOrchestration();

		using var provider = services.BuildServiceProvider();

		var coordinator = provider.GetService<ISagaCoordinator>();

		coordinator.ShouldNotBeNull(
			"a consumer who registers a persistent saga backend and orchestration must be able to run sagas. "
			+ "Failing to construct here means the coordinator's non-keyed ISagaStore dependency is satisfied "
			+ "only by the in-memory opt-in, which is documented as never a production default.");

		// The coordinator holding SOME store is not enough: an in-memory store leaking in as the default
		// resolves, starts, and loses every saga on restart.
		provider.GetRequiredService<ISagaStore>()
			.ShouldBeOfType<Excalibur.Saga.Postgres.PostgresSagaStore>();
	}
}
