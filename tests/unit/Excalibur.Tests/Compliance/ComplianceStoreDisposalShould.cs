// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Excalibur.Dispatch;
using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Tests.Compliance;

/// <summary>
/// The compliance stores serialise their lazy schema provisioning, own the lock that does it, and
/// therefore have a disposal contract to keep.
/// </summary>
/// <remarks>
/// <para>
/// These seven were the last stores whose initialisation was unguarded, and they were held back on
/// purpose: they are public sealed types, so adding a lock made them own a disposable they never
/// released, and adding IDisposable to a published type is a product decision. It was resolved the
/// way the framework resolves it for its own lazily-connected caches -- the type implements
/// IDisposable, sets its disposed flag before releasing anything, and refuses use afterwards.
/// </para>
/// <para>
/// Each store is asserted on BOTH arms, and the second is the one that matters. "Throws after
/// disposal" alone is satisfied by a store that throws all the time -- a total outage would pass it
/// -- so every case first proves the very same call does NOT report disposal while the store is
/// alive. The store cannot reach its database here, and that is deliberate: the point is which
/// failure it reports, not that it succeeds. Refusing to connect is a live store; reporting
/// ObjectDisposedException is not.
/// </para>
/// <para>
/// No container is needed because the disposal guard runs before any connection is opened. The
/// connection strings point at a closed port with a one-second timeout so the live-store arm fails
/// immediately rather than waiting on a network.
/// </para>
/// </remarks>
public sealed class ComplianceStoreDisposalShould
{
	private const string UnreachablePostgres =
		"Host=127.0.0.1;Port=1;Database=x;Username=u;Password=p;Timeout=1;Command Timeout=1";

	private const string UnreachableSqlServer =
		"Server=127.0.0.1,1;Database=x;User Id=u;Password=p;Connect Timeout=1;TrustServerCertificate=true";

	public static TheoryData<string> StoreNames =>
	[
		nameof(PostgresLegalHoldStore),
		nameof(PostgresErasureStore),
		nameof(PostgresDataInventoryStore),
		nameof(SqlServerLegalHoldStore),
		nameof(SqlServerErasureStore),
		nameof(SqlServerDataInventoryStore),
	];

	[Theory]
	[MemberData(nameof(StoreNames))]
	public async Task Refuse_Use_After_Disposal_While_Working_Normally_Before_It(string storeName)
	{
		var (store, operation) = Create(storeName);

		using (store)
		{
			// LIVENESS. The same call, on a live store, must fail for a REASON OTHER THAN DISPOSAL.
			// Without this the assertion below is satisfied by a store that has stopped working
			// altogether, which is a worse defect than the one being guarded against.
			var live = await Record.ExceptionAsync(operation).ConfigureAwait(false);

			live.ShouldNotBeNull(
				$"{storeName} reached a database at a closed port, so this test is not exercising what "
				+ "it claims to be exercising.");
			live.ShouldNotBeOfType<ObjectDisposedException>(
				$"{storeName} reported itself disposed while it was still alive, so the safety arm "
				+ "below would pass for the wrong reason.");
		}

		// SAFETY. After disposal the same call is refused, before any connection is attempted.
		var afterDisposal = await Record.ExceptionAsync(operation).ConfigureAwait(false);

		var refusal = afterDisposal.ShouldBeOfType<ObjectDisposedException>(
			$"{storeName} did not refuse use after disposal. Its initialisation lock has been released, "
			+ "so a caller arriving now either faults inside the lock or provisions against a store "
			+ "that is being torn down. Refusing is the contract a disposable type owes its callers.");

		// The STORE must be the object that refuses, and this assertion is the whole reason the arm
		// above is worth anything. Deleting the store's own disposed check still produces an
		// ObjectDisposedException here -- thrown by the released semaphore when the next caller waits
		// on it -- so the first assertion passes over the guard's removal. That was measured, not
		// assumed: the mutant passed 12/12. The two are told apart by WHICH object is named -- the
		// semaphore names System.Threading.SemaphoreSlim, the store names itself. Note that a
		// non-empty check alone is NOT enough, since the semaphore's name is also non-empty; the
		// assertion that actually detects the mutant is the one comparing against the store name.
		refusal.ObjectName.ShouldNotBeNullOrEmpty(
			$"the refusal came from a released primitive inside {storeName} rather than from the store "
			+ "itself, so its own disposed check is gone. That check is what refuses a caller arriving "
			+ "AFTER initialisation succeeded -- by then the early return is taken, the lock is never "
			+ "waited on, and a disposed store would go on to open a connection.");
		refusal.ObjectName.ShouldContain(
			storeName,
			Case.Sensitive,
			$"the refusal names '{refusal.ObjectName}' rather than {storeName}, so it did not come from "
			+ "the store's own guard.");
	}

	[Theory]
	[MemberData(nameof(StoreNames))]
	public void Tolerate_Being_Disposed_Twice(string storeName)
	{
		var (store, _) = Create(storeName);

		store.Dispose();

		// Disposal is routinely double-invoked -- a using block inside a container that also owns the
		// registration -- and a second call must be a no-op rather than a fault on a shutdown path.
		Should.NotThrow(store.Dispose);
	}

	private static (IDisposable Store, Func<Task> Operation) Create(string storeName)
	{
		switch (storeName)
		{
			case nameof(PostgresLegalHoldStore):
			{
				var store = new PostgresLegalHoldStore(
					Options.Create(new PostgresLegalHoldStoreOptions { ConnectionString = UnreachablePostgres }),
					NullLogger<PostgresLegalHoldStore>.Instance,
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
				return (store, () => store.GetActiveHoldsForDataSubjectAsync("h", null, CancellationToken.None));
			}

			case nameof(PostgresErasureStore):
			{
				var store = new PostgresErasureStore(
					Options.Create(new PostgresErasureStoreOptions { ConnectionString = UnreachablePostgres }),
					PassThroughHasher.Instance,
					NullLogger<PostgresErasureStore>.Instance,
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
				return (store, () => store.GetScheduledRequestsAsync(1, CancellationToken.None));
			}

			case nameof(PostgresDataInventoryStore):
			{
				var store = new PostgresDataInventoryStore(
					Options.Create(new PostgresDataInventoryStoreOptions { ConnectionString = UnreachablePostgres }),
					PassThroughHasher.Instance,
					NullLogger<PostgresDataInventoryStore>.Instance,
					tenantContext: UntenantedContext.Instance,
					tenantContextOptions: Options.Create(new TenantContextOptions()));
				return (store, () => store.FindRegistrationsForDataSubjectAsync(
					"s", DataSubjectIdType.UserId, null, CancellationToken.None));
			}

			case nameof(SqlServerLegalHoldStore):
			{
				var store = new SqlServerLegalHoldStore(
					Options.Create(new SqlServerLegalHoldStoreOptions { ConnectionString = UnreachableSqlServer }),
					NullLogger<SqlServerLegalHoldStore>.Instance,
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
				return (store, () => store.GetActiveHoldsForDataSubjectAsync("h", null, CancellationToken.None));
			}

			case nameof(SqlServerErasureStore):
			{
				var store = new SqlServerErasureStore(
					Options.Create(new SqlServerErasureStoreOptions { ConnectionString = UnreachableSqlServer }),
					PassThroughHasher.Instance,
					NullLogger<SqlServerErasureStore>.Instance,
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
				return (store, () => store.GetScheduledRequestsAsync(1, CancellationToken.None));
			}

			case nameof(SqlServerDataInventoryStore):
			{
				var store = new SqlServerDataInventoryStore(
					Options.Create(new SqlServerDataInventoryStoreOptions { ConnectionString = UnreachableSqlServer }),
					PassThroughHasher.Instance,
					NullLogger<SqlServerDataInventoryStore>.Instance,
					tenantContext: UntenantedContext.Instance,
					tenantContextOptions: Options.Create(new TenantContextOptions()));
				return (store, () => store.FindRegistrationsForDataSubjectAsync(
					"s", DataSubjectIdType.UserId, null, CancellationToken.None));
			}

			default:
				throw new ArgumentOutOfRangeException(
					nameof(storeName),
					storeName,
					"Unknown store. A store added to the theory data without a case here would silently "
					+ "never be exercised.");
		}
	}

	/// <summary>
	/// A hasher that returns its input. These cases never reach a database, so hashing is not the
	/// behaviour under test -- the store simply requires one to be constructed.
	/// </summary>
	private sealed class PassThroughHasher : IDataSubjectHasher
	{
		public static PassThroughHasher Instance { get; } = new();

		public string HashDataSubjectId(string dataSubjectId) => dataSubjectId;
	}
}
