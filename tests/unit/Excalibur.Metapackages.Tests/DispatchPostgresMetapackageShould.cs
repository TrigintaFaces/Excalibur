// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Metapackages.Tests;

/// <summary>
/// Registration locks for the <c>AddDispatchWithPostgres</c> one-liner.
/// </summary>
/// <remarks>
/// <para>
/// This metapackage documented an outbox and registered none. Unlike its SQL Server counterpart the fix
/// was to correct the documentation rather than to wire one: the PostgreSQL outbox store is constructed
/// from an <c>Excalibur.Data.IDb</c>, the framework ships no concrete implementation of that interface,
/// and a connection string alone cannot build one — so wiring it here would have registered a store that
/// throws on resolve. The outbox prerequisite validator resolves the store as a hosted service, so the
/// failure would have surfaced at host startup rather than at first use.
/// </para>
/// <para>
/// <see cref="NotRegisterAnOutboxStore"/> is therefore a deliberate lock on the corrected documentation,
/// not an accepted gap. When the underlying store becomes constructible from a connection string, that
/// test is the one that should fail and be rewritten to assert the store resolves.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metapackages")]
public sealed class DispatchPostgresMetapackageShould : UnitTestBase
{
	// Syntactically valid, never connected to.
	private const string ConnectionString =
		"Host=localhost;Database=excalibur_metapackage_tests;Username=excalibur";

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchWithPostgres(ConnectionString);
		return services.BuildServiceProvider();
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
		// The liveness arm: the one-liner must actually deliver the event sourcing it documents, so
		// NotRegisterAnOutboxStore below cannot be satisfied by a call that registers nothing at all.
		using var provider = BuildProvider();

		provider.GetKeyedService<Excalibur.EventSourcing.IEventStore>("default").ShouldNotBeNull();
	}

	[Fact]
	public void NotRegisterAnOutboxStore()
	{
		// Locks the corrected documentation: this entry point no longer claims an outbox, and must not
		// acquire one silently either. A store registered here would be worse than none — it would throw
		// on resolve and take the host down at startup.
		using var provider = BuildProvider();

		provider.GetService<IOutboxStore>().ShouldBeNull();
	}

	[Fact]
	public void StartNoBackgroundDeliveryService()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchWithPostgres(ConnectionString);

		services.Any(descriptor => descriptor.ImplementationType?.Name == "OutboxBackgroundService")
			.ShouldBeFalse(
				"no outbox is registered here, so nothing should be draining one.");
	}
}
