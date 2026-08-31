// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.Options;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Registration locks for the <c>AddDispatchWithSqlServer</c> one-liner.
/// </summary>
/// <remarks>
/// These assert the <em>emitted registration</em> — every case builds a real <see cref="ServiceProvider"/>
/// from the public entry point and resolves what the package documents, rather than asserting that some
/// inner call was made. The metapackage advertised an outbox in its XML documentation while registering
/// none; a test that only checked "a call happened" would not have caught that, and neither would one
/// that never left the service collection.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class DispatchSqlServerMetapackageShould : UnitTestBase
{
	// Syntactically valid, never connected to: every assertion here resolves services, and the store's
	// connection factory is not invoked until a message is actually staged.
	private const string ConnectionString =
		"Server=localhost;Database=ExcaliburMetapackageTests;Trusted_Connection=True;TrustServerCertificate=True";

	private static ServiceProvider BuildProvider(Action<IServiceCollection>? extra = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchWithSqlServer(ConnectionString);
		extra?.Invoke(services);
		return services.BuildServiceProvider();
	}

	[Fact]
	public void RegisterAnOutboxStoreThatActuallyResolves()
	{
		// The defect this locks: the XML doc said "event sourcing and outbox" and the body registered no
		// outbox at all, so a consumer believed messages were staged durably and they were not.
		using var provider = BuildProvider();

		var store = provider.GetService<IOutboxStore>();

		store.ShouldNotBeNull(
			"AddDispatchWithSqlServer documents an outbox; a consumer must be able to resolve one.");
	}

	[Fact]
	public void RegisterAnOutboxStoreBackedBySqlServer()
	{
		// Resolving *something* is not enough — the one-liner promises a SQL Server outbox specifically.
		// Capability lookup rather than a cast: the store is wrapped in a telemetry decorator, and casting
		// would see only the outermost type.
		using var provider = BuildProvider();

		var store = provider.GetRequiredService<IOutboxStore>();

		store.GetService(typeof(IMultiTransportOutboxStore)).ShouldNotBeNull();
		store.GetService(typeof(SqlServerOutboxStore)).ShouldBeOfType<SqlServerOutboxStore>();
	}

	[Fact]
	public void RegisterTheDispatcher()
	{
		using var provider = BuildProvider();

		provider.GetService<IDispatcher>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterTheEventStore()
	{
		using var provider = BuildProvider();

		provider.GetKeyedService<Excalibur.EventSourcing.IEventStore>("default").ShouldNotBeNull();
	}

	[Fact]
	public void StartNoBackgroundDeliveryService()
	{
		// The deliberate half of the design: the one-liner stages durably but does not start draining, so
		// adding it cannot change the runtime profile of a host that never asked for a delivery loop.
		// Paired with RegisterAnOutboxStoreThatActuallyResolves — on its own this assertion would be
		// satisfied by registering no outbox whatsoever, which is the very defect being fixed.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchWithSqlServer(ConnectionString);

		services.Any(descriptor => descriptor.ImplementationType?.Name == "OutboxBackgroundService")
			.ShouldBeFalse(
				"background delivery is opt-in; the one-liner must not start a drain loop on its own.");
	}

	[Fact]
	public void ComposeWithALaterExplicitOutboxDeclaration()
	{
		// A consumer who needs to tune the outbox must not have to abandon the one-line entry point. The
		// later, more specific declaration wins.
		using var provider = BuildProvider(services =>
			services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
				outbox.UseSqlServer(sql => sql
					.ConnectionString(ConnectionString)
					.SchemaName("Messaging")))));

		provider.GetService<IOutboxStore>().ShouldNotBeNull();
		provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value.Tables.SchemaName
			.ShouldBe("Messaging");
	}
}
