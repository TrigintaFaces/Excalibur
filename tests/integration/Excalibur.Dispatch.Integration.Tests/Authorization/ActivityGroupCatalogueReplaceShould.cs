// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;
using Excalibur.Data.SqlServer.Authorization;

using Microsoft.Data.SqlClient;
using MsOptions = Microsoft.Extensions.Options.Options;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Locks the whole-catalogue replace against real databases: what it leaves behind, what a concurrent reader
/// can observe, how two concurrent replaces combine, and which catalogue it reaches.
/// </summary>
/// <remarks>
/// Every property here is a property of the database session, not of the store's C#: the lock, the
/// transaction and the isolation each exist only on a real server, so none of these arms could be written
/// against a fake. The tables are provisioned from the shipped scripts.
/// </remarks>
public abstract class ActivityGroupCatalogueReplaceShould : IAsyncLifetime
{
	/// <summary>The second schema, used to prove a catalogue replace reaches only its own schema.</summary>
	protected const string OtherSchema = "authz_other";

	private const int CatalogueSize = 300;

	/// <summary>Provisions the table in <paramref name="schema"/> from the shipped script.</summary>
	/// <param name="schema">The schema to provision.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table exists.</returns>
	protected abstract Task ProvisionAsync(string schema, CancellationToken cancellationToken);

	/// <summary>Empties the table in <paramref name="schema"/>.</summary>
	/// <param name="schema">The schema to empty.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table is empty.</returns>
	protected abstract Task TruncateAsync(string schema, CancellationToken cancellationToken);

	/// <summary>Creates a store over its own connection, addressing <paramref name="schema"/>.</summary>
	/// <param name="schema">The schema the store addresses.</param>
	/// <returns>The store and the database it owns, which the caller disposes.</returns>
	protected abstract (IActivityGroupStore Store, IDisposable Db) CreateStore(string schema = "authz");

	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		var ct = TestContext.Current.CancellationToken;

		foreach (var schema in new[] { "authz", OtherSchema })
		{
			await ProvisionAsync(schema, ct).ConfigureAwait(false);
			await TruncateAsync(schema, ct).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// SAFETY and LIVENESS: afterwards the store holds exactly the catalogue. A tenant it does not name has no
	/// groups, and the tenants that held groups before are reported.
	/// </summary>
	[Fact]
	public async Task LeaveExactlyTheCatalogue_AndReportThePreviousTenants()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		_ = await store.CreateActivityGroupAsync("dropped", "Admins", "Read", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync("kept", "Admins", "Read", ct).ConfigureAwait(false);

		var previous = await store.ReplaceAllActivityGroupsAsync(
			new ActivityGroupCatalogue(
			[
				new ActivityGroupEntry("kept", "Admins", "Write"),
				new ActivityGroupEntry("new", "Support", "Read"),
			]),
			ct).ConfigureAwait(false);

		previous.OrderBy(static t => t, StringComparer.Ordinal).ToArray().ShouldBe(["dropped", "kept"]);
		(await store.FindActivityGroupsAsync("dropped", ct).ConfigureAwait(false))
			.ShouldBeEmpty("a tenant the catalogue does not name must have no groups afterwards");
		(await store.FindActivityGroupsAsync("kept", ct).ConfigureAwait(false)).Values.Single()
			.ShouldBe(["Write"], customMessage: "a kept tenant holds the catalogue's activities, not its old ones");
		(await store.FindActivityGroupsAsync("new", ct).ConfigureAwait(false)).ShouldHaveSingleItem();
	}

	/// <summary>
	/// SAFETY: a reader on another connection observes either the whole previous catalogue or the whole new
	/// one, never an empty or partial one, while replaces run.
	/// </summary>
	[Fact]
	public async Task NeverShowAConcurrentReaderAPartialCatalogue()
	{
		var ct = TestContext.Current.CancellationToken;
		var first = Catalogue("First");
		var second = Catalogue("Second");

		var (writer, writerDb) = CreateStore();
		using var ownedWriterDb = writerDb;
		_ = await writer.ReplaceAllActivityGroupsAsync(first, ct).ConfigureAwait(false);

		using var stop = new CancellationTokenSource();
		var torn = 0;
		var reads = 0;

		var readers = Enumerable.Range(0, 3).Select(_ => Task.Run(
			async () =>
			{
				var (reader, readerDb) = CreateStore();
				using var ownedReaderDb = readerDb;

				while (!stop.IsCancellationRequested)
				{
					var groups = await reader.FindActivityGroupsAsync("t", ct).ConfigureAwait(false);
					_ = Interlocked.Increment(ref reads);

					if (groups.Count != 1 || groups.Values.Single().Count != CatalogueSize)
					{
						_ = Interlocked.Increment(ref torn);
					}
				}
			},
			ct)).ToArray();

		for (var i = 0; i < 20; i++)
		{
			_ = await writer.ReplaceAllActivityGroupsAsync(i % 2 == 0 ? second : first, ct).ConfigureAwait(false);
		}

		await stop.CancelAsync().ConfigureAwait(false);
		await Task.WhenAll(readers).ConfigureAwait(false);

		reads.ShouldBeGreaterThan(0, "the readers never ran, so nothing was observed");
		torn.ShouldBe(0, $"{torn} of {reads} reads observed an empty or partial catalogue");
	}

	/// <summary>
	/// SAFETY: two replaces running at once leave one whole catalogue, never the union of both.
	/// </summary>
	/// <remarks>
	/// Without the lock that orders them, the second replace's delete cannot see the rows the first one has
	/// just inserted, so both catalogues survive together -- a catalogue no authority ever sent.
	/// </remarks>
	[Fact]
	public async Task LeaveOneWholeCatalogueWhenTwoReplacesRunAtOnce()
	{
		var ct = TestContext.Current.CancellationToken;

		for (var round = 0; round < 10; round++)
		{
			var a = Catalogue($"A{round}");
			var b = Catalogue($"B{round}");

			var (storeA, dbA) = CreateStore();
			var (storeB, dbB) = CreateStore();
			using (dbA)
			using (dbB)
			{
				await Task.WhenAll(
					Task.Run(() => storeA.ReplaceAllActivityGroupsAsync(a, ct), ct),
					Task.Run(() => storeB.ReplaceAllActivityGroupsAsync(b, ct), ct)).ConfigureAwait(false);
			}

			var (check, checkDb) = CreateStore();
			using var ownedCheckDb = checkDb;
			var groups = await check.FindActivityGroupsAsync("t", ct).ConfigureAwait(false);

			groups.Count.ShouldBe(1, $"round {round}: both catalogues survived together: {string.Join(", ", groups.Keys)}");
			groups.Values.Single().Count.ShouldBe(CatalogueSize);
		}
	}

	/// <summary>
	/// SAFETY: a replace through a store configured for another schema leaves this schema's catalogue alone,
	/// which is what lets two applications share one database.
	/// </summary>
	[Fact]
	public async Task ReachOnlyTheConfiguredSchema()
	{
		var ct = TestContext.Current.CancellationToken;
		var (home, homeDb) = CreateStore();
		var (other, otherDb) = CreateStore(OtherSchema);
		using var ownedHomeDb = homeDb;
		using var ownedOtherDb = otherDb;

		_ = await home.CreateActivityGroupAsync("t", "Home", "Read", ct).ConfigureAwait(false);

		var previous = await other.ReplaceAllActivityGroupsAsync(
			new ActivityGroupCatalogue([new ActivityGroupEntry("t", "Other", "Read")]), ct).ConfigureAwait(false);

		previous.ShouldBeEmpty("the other schema held nothing, so nothing of this one was removed");
		(await home.FindActivityGroupsAsync("t", ct).ConfigureAwait(false)).Keys.ShouldHaveSingleItem()
			.ShouldContain("Home");

		// LIVENESS: the other schema really was written.
		(await other.FindActivityGroupsAsync("t", ct).ConfigureAwait(false)).Keys.ShouldHaveSingleItem()
			.ShouldContain("Other");
	}

	/// <summary>
	/// The replace refuses to run inside an ambient transaction, and changes nothing.
	/// </summary>
	[Fact]
	public async Task RefuseToRunInsideAnAmbientTransaction()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;
		_ = await store.CreateActivityGroupAsync("t", "Before", "Read", ct).ConfigureAwait(false);

		using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(
				() => store.ReplaceAllActivityGroupsAsync(
					new ActivityGroupCatalogue([new ActivityGroupEntry("t", "After", "Read")]), ct)).ConfigureAwait(false);
		}

		(await store.FindActivityGroupsAsync("t", ct).ConfigureAwait(false)).Keys.ShouldHaveSingleItem()
			.ShouldContain("Before");
	}

	private static ActivityGroupCatalogue Catalogue(string group) =>
		new(Enumerable.Range(0, CatalogueSize).Select(i => new ActivityGroupEntry("t", group, $"activity-{i}")));
}

/// <summary>Runs the catalogue-replace arms against SQL Server.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SqlServerActivityGroupCatalogueReplaceShould(SqlServerFixture fixture)
	: ActivityGroupCatalogueReplaceShould
{
	/// <inheritdoc />
	protected override Task ProvisionAsync(string schema, CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.ProvisionSqlServerAsync(fixture.ConnectionString, schema, cancellationToken);

	/// <inheritdoc />
	protected override Task TruncateAsync(string schema, CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.TruncateSqlServerAsync(fixture.ConnectionString, schema, cancellationToken);

	/// <inheritdoc />
	protected override (IActivityGroupStore Store, IDisposable Db) CreateStore(string schema = "authz")
	{
		var db = new DomainDb(new SqlConnection(fixture.ConnectionString));
		return (new SqlServerActivityGroupStore(db, MsOptions.Create(new SqlServerAuthorizationOptions { SchemaName = schema })), db);
	}
}

/// <summary>Runs the catalogue-replace arms against Postgres.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresActivityGroupCatalogueReplaceShould(PostgresFixture fixture)
	: ActivityGroupCatalogueReplaceShould
{
	/// <inheritdoc />
	protected override Task ProvisionAsync(string schema, CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.ProvisionPostgresAsync(fixture.ConnectionString, schema, cancellationToken);

	/// <inheritdoc />
	protected override Task TruncateAsync(string schema, CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.TruncatePostgresAsync(fixture.ConnectionString, schema, cancellationToken);

	/// <inheritdoc />
	protected override (IActivityGroupStore Store, IDisposable Db) CreateStore(string schema = "authz")
	{
		var db = new DomainDb(new NpgsqlConnection(fixture.ConnectionString));
		return (new PostgresActivityGroupStore(db, MsOptions.Create(new PostgresAuthorizationOptions { SchemaName = schema })), db);
	}
}
