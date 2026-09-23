// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;
using Excalibur.Data.SqlServer.Authorization;

using Microsoft.Data.SqlClient;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Locks the activity-group grant replace against real databases: what it revokes, what it leaves alone, what
/// it refuses before touching anything, and how two concurrent replaces combine.
/// </summary>
/// <remarks>
/// Every property here is a property of the database session, not of the store's C#: the lock, the transaction
/// and the isolation each exist only on a real server, so none of these arms could be written against a fake.
/// </remarks>
public abstract class ActivityGroupGrantReplaceShould : IAsyncLifetime
{
	/// <summary>
	/// The activity-group grant type, spelled out rather than taken from <c>GrantType.ActivityGroup</c>:
	/// that constant lives in a package this suite does not reference, and the VALUE is what the stores match on.
	/// </summary>
	protected const string ActivityGroupType = "ActivityGroup";

	/// <summary>A grant type the replace is NOT scoped to, used to prove it reaches only its own.</summary>
	protected const string OtherType = "Role";

	private const int SnapshotSize = 100;

	/// <summary>Creates a store over its own connection.</summary>
	/// <returns>The store and the database it owns, which the caller disposes.</returns>
	protected abstract (IActivityGroupGrantStore Store, IDisposable Db) CreateStore();

	/// <summary>Creates the grant table if it is not there, and empties it.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table exists and is empty.</returns>
	protected abstract Task ResetAsync(CancellationToken cancellationToken);

	/// <summary>Reads the qualifiers a user holds for a grant type, straight from the table.</summary>
	/// <param name="userId">The user to read.</param>
	/// <param name="grantType">The grant type to read.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The qualifiers, in no particular order.</returns>
	protected abstract Task<IReadOnlyList<string>> QualifiersAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken);

	/// <inheritdoc />
	public async ValueTask InitializeAsync() =>
		await ResetAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// SAFETY and LIVENESS: afterwards the store holds exactly the snapshot's grants of that type. A user it
	/// does not name holds none, and the users that held one before are reported.
	/// </summary>
	/// <remarks>
	/// The seeding runs through <c>InsertActivityGroupGrantAsync</c> and the reading through raw SQL against
	/// the same table, so an insert that addressed a DIFFERENT table from the one the deletes and reads use —
	/// which is what a quoted, differently-cased identifier does on PostgreSQL — fails here rather than
	/// silently writing rows nothing can see.
	/// </remarks>
	[Fact]
	public async Task LeaveExactlyTheSnapshot_AndReportThePreviousUsers()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		await SeedAsync(store, "dropped-user", "Billing", ActivityGroupType, ct);
		await SeedAsync(store, "kept-user", "Billing", ActivityGroupType, ct);

		var previous = await Replacement(store).ReplaceActivityGroupGrantsAsync(
			ActivityGroupType,
			Snapshot(("kept-user", "Support"), ("new-user", "Billing")),
			ct);

		previous.OrderBy(static u => u, StringComparer.Ordinal).ToArray()
			.ShouldBe(["dropped-user", "kept-user"]);
		(await QualifiersAsync("dropped-user", ActivityGroupType, ct))
			.ShouldBeEmpty("a user the snapshot does not name holds no grants of that type afterwards");
		(await QualifiersAsync("kept-user", ActivityGroupType, ct)).ShouldBe(["Support"]);
		(await QualifiersAsync("new-user", ActivityGroupType, ct)).ShouldBe(["Billing"]);
	}

	/// <summary>
	/// SAFETY: an estate-wide replace of one grant type leaves grants of every other type alone.
	/// </summary>
	[Fact]
	public async Task LeaveGrantsOfAnotherTypeAlone()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		await SeedAsync(store, "user-1", "Elsewhere", OtherType, ct);

		_ = await Replacement(store).ReplaceActivityGroupGrantsAsync(
			ActivityGroupType, Snapshot(("user-1", "Support")), ct);

		(await QualifiersAsync("user-1", OtherType, ct)).ShouldBe(["Elsewhere"]);
		(await QualifiersAsync("user-1", ActivityGroupType, ct)).ShouldBe(["Support"]);
	}

	/// <summary>
	/// The PER-USER replace accepts an EMPTY snapshot, revokes exactly that user's grants of the type, and
	/// leaves every other user's rows untouched.
	/// </summary>
	/// <remarks>
	/// The asymmetry with the estate-wide member is the requirement: a user who now holds no activity-group
	/// grants is an ordinary state and every grant they held must go, or a revocation the authority performed
	/// never takes effect here.
	/// </remarks>
	[Fact]
	public async Task AcceptAnEmptyPerUserSnapshot_AndRevokeOnlyThatUsersGrants()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		await SeedAsync(store, "user-1", "Billing", ActivityGroupType, ct);
		await SeedAsync(store, "user-1", "Support", ActivityGroupType, ct);
		await SeedAsync(store, "user-2", "Billing", ActivityGroupType, ct);
		await SeedAsync(store, "user-1", "Elsewhere", OtherType, ct);

		var previous = await Replacement(store).ReplaceActivityGroupGrantsForUserAsync(
			"user-1", ActivityGroupType, Snapshot(), ct);

		previous.ShouldBe(["user-1"]);
		(await QualifiersAsync("user-1", ActivityGroupType, ct))
			.ShouldBeEmpty("an empty per-user snapshot revokes every grant of that type the user held");

		// LIVENESS on both other axes: a replace that simply emptied the table would satisfy the line above.
		(await QualifiersAsync("user-2", ActivityGroupType, ct)).ShouldBe(["Billing"]);
		(await QualifiersAsync("user-1", OtherType, ct)).ShouldBe(["Elsewhere"]);
	}

	/// <summary>
	/// SAFETY: an empty ESTATE-WIDE snapshot is refused, and nothing is removed.
	/// </summary>
	[Fact]
	public async Task RefuseAnEmptyEstateSnapshot_WithoutRemovingAnything()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		await SeedAsync(store, "user-1", "Billing", ActivityGroupType, ct);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => Replacement(store).ReplaceActivityGroupGrantsAsync(ActivityGroupType, Snapshot(), ct));

		(await QualifiersAsync("user-1", ActivityGroupType, ct)).ShouldBe(["Billing"]);
	}

	/// <summary>
	/// SAFETY: a snapshot mixing grant types is refused before the transaction opens, so nothing is deleted.
	/// </summary>
	[Fact]
	public async Task RefuseASnapshotMixingGrantTypes_WithoutDeletingAnything()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		await SeedAsync(store, "user-1", "Billing", ActivityGroupType, ct);

		var mixed = new ActivityGroupGrantSnapshot(
		[
			Entry("user-1", "Support", ActivityGroupType),
			Entry("user-1", "Elsewhere", OtherType),
		]);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => Replacement(store).ReplaceActivityGroupGrantsAsync(ActivityGroupType, mixed, ct));

		(await QualifiersAsync("user-1", ActivityGroupType, ct)).ShouldBe(["Billing"]);
	}

	/// <summary>
	/// SAFETY: two replaces running at once both complete, and one whole snapshot remains — never the union.
	/// </summary>
	/// <remarks>
	/// <b>This is the arm the lock exists for.</b> Without the lock that orders them, the second replace's
	/// delete cannot see the rows the first has just inserted, so it inserts its own beside them: on a table
	/// with the grant's primary key that is a key violation, and on one without it both snapshots survive
	/// together — a set no authority ever sent. Either way this arm goes red.
	/// </remarks>
	[Fact]
	public async Task LeaveOneWholeSnapshotWhenTwoReplacesRunAtOnce()
	{
		var ct = TestContext.Current.CancellationToken;

		for (var round = 0; round < 5; round++)
		{
			await ResetAsync(ct);

			var a = Wide($"A{round}");
			var b = Wide($"B{round}");

			var (storeA, dbA) = CreateStore();
			var (storeB, dbB) = CreateStore();
			using (dbA)
			using (dbB)
			{
				await Task.WhenAll(
					Task.Run(() => Replacement(storeA).ReplaceActivityGroupGrantsAsync(ActivityGroupType, a, ct), ct),
					Task.Run(() => Replacement(storeB).ReplaceActivityGroupGrantsAsync(ActivityGroupType, b, ct), ct));
			}

			var held = await QualifiersAsync("t", ActivityGroupType, ct);

			held.Count.ShouldBe(
				SnapshotSize,
				$"round {round}: {held.Count} grants survived, so the two snapshots were not serialized");
			held.Select(static q => q[..q.IndexOf('-', StringComparison.Ordinal)]).Distinct(StringComparer.Ordinal)
				.Count().ShouldBe(1, $"round {round}: both snapshots survived together");
		}
	}

	/// <summary>
	/// SAFETY: a reader on another connection observes either the whole previous set of a user's grants or the
	/// whole new one, never a partial one, while replaces run.
	/// </summary>
	[Fact]
	public async Task NeverShowAConcurrentReaderAPartialGrantSet()
	{
		var ct = TestContext.Current.CancellationToken;
		var first = Wide("First");
		var second = Wide("Second");

		var (writer, writerDb) = CreateStore();
		using var ownedWriterDb = writerDb;
		_ = await Replacement(writer).ReplaceActivityGroupGrantsAsync(ActivityGroupType, first, ct);

		using var stop = new CancellationTokenSource();
		var torn = 0;
		var reads = 0;

		var readers = Enumerable.Range(0, 2).Select(_ => Task.Run(
			async () =>
			{
				while (!stop.IsCancellationRequested)
				{
					var held = await QualifiersAsync("t", ActivityGroupType, ct).ConfigureAwait(false);
					_ = Interlocked.Increment(ref reads);

					if (held.Count != SnapshotSize)
					{
						_ = Interlocked.Increment(ref torn);
					}
				}
			},
			ct)).ToArray();

		for (var i = 0; i < 10; i++)
		{
			_ = await Replacement(writer).ReplaceActivityGroupGrantsAsync(
				ActivityGroupType, i % 2 == 0 ? second : first, ct);
		}

		await stop.CancelAsync();
		await Task.WhenAll(readers);

		reads.ShouldBeGreaterThan(0, "the readers never ran, so nothing was observed");
		torn.ShouldBe(0, $"{torn} of {reads} reads observed a partial grant set");
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

		await SeedAsync(store, "user-1", "Before", ActivityGroupType, ct);

		using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(
				() => Replacement(store).ReplaceActivityGroupGrantsAsync(
					ActivityGroupType, Snapshot(("user-1", "After")), ct));
		}

		(await QualifiersAsync("user-1", ActivityGroupType, ct)).ShouldBe(["Before"]);
	}

	private static IActivityGroupGrantReplacement Replacement(IActivityGroupGrantStore store) =>
		(IActivityGroupGrantReplacement)store;

	private static ActivityGroupGrantEntry Entry(string userId, string qualifier, string grantType) =>
		new(userId, userId, "acme", grantType, qualifier, null, "granter");

	private static ActivityGroupGrantSnapshot Snapshot(params (string UserId, string Qualifier)[] grants) =>
		new(grants.Select(g => Entry(g.UserId, g.Qualifier, ActivityGroupType)));

	private static ActivityGroupGrantSnapshot Wide(string prefix) =>
		new(Enumerable.Range(0, SnapshotSize)
			.Select(i => Entry("t", $"{prefix}-{i}", ActivityGroupType)));

	private static Task SeedAsync(
		IActivityGroupGrantStore store,
		string userId,
		string qualifier,
		string grantType,
		CancellationToken cancellationToken) =>
		store.InsertActivityGroupGrantAsync(
			userId, userId, "acme", grantType, qualifier, null, "granter", cancellationToken);
}

/// <summary>Runs the grant-replace arms against SQL Server.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SqlServerActivityGroupGrantReplaceShould(SqlServerFixture fixture)
	: ActivityGroupGrantReplaceShould
{
	/// <inheritdoc />
	protected override (IActivityGroupGrantStore Store, IDisposable Db) CreateStore()
	{
		var db = new DomainDb(new SqlConnection(fixture.ConnectionString));
		return (new SqlServerGrantStore(db), db);
	}

	/// <inheritdoc />
	protected override async Task ResetAsync(CancellationToken cancellationToken)
	{
		await GrantSchema.ProvisionSqlServerAsync(fixture.ConnectionString, cancellationToken).ConfigureAwait(false);
		await GrantSchema.TruncateSqlServerAsync(fixture.ConnectionString, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override Task<IReadOnlyList<string>> QualifiersAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken) =>
		GrantSchema.SqlServerQualifiersAsync(fixture.ConnectionString, userId, grantType, cancellationToken);
}

/// <summary>Runs the grant-replace arms against Postgres.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresActivityGroupGrantReplaceShould(PostgresFixture fixture)
	: ActivityGroupGrantReplaceShould
{
	/// <inheritdoc />
	protected override (IActivityGroupGrantStore Store, IDisposable Db) CreateStore()
	{
		var db = new DomainDb(new NpgsqlConnection(fixture.ConnectionString));
		return (new PostgresGrantStore(db), db);
	}

	/// <inheritdoc />
	protected override async Task ResetAsync(CancellationToken cancellationToken)
	{
		await GrantSchema.ProvisionPostgresAsync(fixture.ConnectionString, cancellationToken).ConfigureAwait(false);
		await GrantSchema.TruncatePostgresAsync(fixture.ConnectionString, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override Task<IReadOnlyList<string>> QualifiersAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken) =>
		GrantSchema.PostgresQualifiersAsync(fixture.ConnectionString, userId, grantType, cancellationToken);
}
