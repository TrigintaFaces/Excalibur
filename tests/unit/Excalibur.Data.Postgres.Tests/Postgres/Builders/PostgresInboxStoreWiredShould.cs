// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Data.Tests.Postgres.Builders;

// Independent regression lock (author != implementer) for 02aqfe — the advertised-but-unwired inbox seam:
//
//   AddExcaliburInbox(inbox => inbox.UsePostgres(...)) is a DOCUMENTED entry point. Before the fix it registered
//   options and a connection factory but NO IInboxStore, so the documented path resolved NOTHING under the
//   "postgres" or "default" key — a consumer wired the inbox and got silence. This is the ADR-336 advertised-but-
//   unwired class: the API exists, the registration is inert.
//
// The sibling composition tests (PostgresCompositionShould) assert that the OPTIONS resolve and that multiple
// Postgres builders coexist — but NOT that the store itself resolves. Options-registered-but-no-store is exactly
// the gap 02aqfe closes, so it needs its own lock.
//
// Construction is connection-FACTORY based (Func<NpgsqlConnection>), so the store resolves without opening a
// connection — this is a pure DI WIRE lock, no live database.
//
// SAFETY + LIVENESS (testing-patterns §3): "resolves non-null" is the liveness half; the safety/non-vacuity half
// is that "default" forwards to the SAME "postgres" instance (not a second, divergent registration) — proving the
// documented default key reaches the real store, not a look-alike.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresInboxStoreWiredShould : UnitTestBase
{
	private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

	[Fact]
	public void ResolveAnIInboxStore_UnderThePostgresAndDefaultKeys_AfterUsePostgres()
	{
		// Arrange + Act — the documented entry point, exactly as a consumer writes it. AddLogging models the
		// ambient logging every real host provides (PostgresInboxStore takes an ILogger<>); without it the store
		// factory would fail on a missing logger, which is a host-composition concern, not the wiring under test.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburInbox(inbox =>
			inbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString).SchemaName("messaging")));

		using var provider = services.BuildServiceProvider();

		// LIVENESS: the documented path resolves a real store under BOTH keys. RED against the pre-fix registration
		// that wired options + a connection factory but no IInboxStore (both would resolve null).
		var postgresStore = provider.GetKeyedService<IInboxStore>("postgres");
		postgresStore.ShouldNotBeNull(
			"AddExcaliburInbox(inbox => inbox.UsePostgres(...)) must register a resolvable IInboxStore under the " +
			"\"postgres\" key. If null, the documented entry point is advertised-but-unwired — the consumer wired " +
			"the inbox and got nothing.");

		var defaultStore = provider.GetKeyedService<IInboxStore>("default");
		defaultStore.ShouldNotBeNull(
			"the \"default\" key must also resolve the inbox store — a consumer that resolves the default inbox " +
			"must reach the Postgres store they configured.");

		// SAFETY / non-vacuity: "default" forwards to the SAME "postgres" instance, not a separate divergent
		// registration. Without this, two independent registrations could both be non-null while the default key
		// pointed at a look-alike that never received the consumer's configuration.
		defaultStore.ShouldBeSameAs(
			postgresStore,
			"the \"default\" inbox key must forward to the same instance as \"postgres\" — otherwise the documented " +
			"default resolves a different store than the one the consumer configured.");
	}
}
