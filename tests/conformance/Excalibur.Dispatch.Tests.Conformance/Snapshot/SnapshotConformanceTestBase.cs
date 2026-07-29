// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Snapshot;

/// <summary>
/// Base class for snapshot store conformance tests.
/// All ISnapshotStore implementations MUST pass this test suite to ensure consistent behavior.
/// Validates requirements: R26.27 (versioning), R26.29 (triggers), R26.32 (cursor maps), R26.47 (consistency).
/// </summary>
/// <remarks>
/// <para>
/// To use this test kit, inherit from this class and implement the abstract methods:
/// </para>
/// <code>
/// public class SqlServerSnapshotStoreConformanceTests : SnapshotConformanceTestBase
/// {
///     protected override Task&lt;ISnapshotStore&gt; CreateSnapshotStoreAsync()
///         => Task.FromResult&lt;ISnapshotStore&gt;(new SqlServerSnapshotStore(connectionString));
///
///     protected override Task DisposeSnapshotStoreAsync()
///     {
///         // Cleanup logic
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </remarks>
[TenantScopedConformance]
public abstract class SnapshotConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// Gets the snapshot store under test.
	/// </summary>
	protected ISnapshotStore? SnapshotStore { get; private set; }

	/// <summary>
	/// Gets the snapshot strategy under test.
	/// </summary>
	protected ISnapshotStrategy? SnapshotStrategy { get; private set; }


	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		SnapshotStore = await CreateSnapshotStoreAsync().ConfigureAwait(false);
		SnapshotStrategy = await CreateSnapshotStrategyAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await DisposeSnapshotStoreAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Creates and initializes the snapshot store implementation to test.
	/// </summary>
	/// <returns>An instance of ISnapshotStore to test.</returns>
	protected abstract Task<ISnapshotStore> CreateSnapshotStoreAsync();

	/// <summary>
	/// Gets the largest snapshot payload the provider under test can store, in bytes.
	/// </summary>
	/// <remarks>
	/// Defaults to 1 MB, which every relational and document provider here carries comfortably. A provider
	/// with a hard platform ceiling below that overrides it: DynamoDB caps a single item at 400 KB, so a
	/// hardcoded 1 MB arm could never pass there however the store was written. Declaring the limit keeps
	/// the arm meaningful on that provider instead of permanently red.
	/// </remarks>
	protected virtual int MaxSnapshotPayloadBytes => 1_000_000;

	/// <summary>
	/// Creates the snapshot strategy to test with.
	/// Return null to skip strategy-specific tests.
	/// </summary>
	/// <returns>An instance of ISnapshotStrategy or null.</returns>
	protected virtual Task<ISnapshotStrategy?> CreateSnapshotStrategyAsync()
		=> Task.FromResult<ISnapshotStrategy?>(new IntervalSnapshotStrategy(100));

	/// <summary>
	/// Cleans up the snapshot store resources.
	/// </summary>
	protected abstract Task DisposeSnapshotStoreAsync();

	// NOTE: there is deliberately NO CreateSnapshotStoreForTenantAsync seam here.
	//
	// An earlier revision added one, on the theory that tenant isolation needs two differently-scoped
	// stores. It does not, and that shape was wrong in a way worth recording so nobody re-adds it:
	//
	//   production registers the store as a SINGLETON and resolves the tenant PER CALL from an ambient
	//   context (InMemorySnapshotStore.GetKey -> TenantScope.FromContext). One store serves every
	//   tenant. Building two stores builds two universes, and on a store whose state is per-instance
	//   the isolation arms then pass without exercising anything at all.
	//
	// The arms below therefore use ONE store — the one every provider already supplies — and vary the
	// AMBIENT scope, which is exactly what OutboxProcessor does on its own drain path. Mirroring the
	// production topology is not a stylistic preference here; it is the difference between an arm that
	// can fail and an arm that cannot.

	/// <summary>
	/// Creates a test snapshot with the given parameters.
	/// Override to customize snapshot creation for your store.
	/// </summary>
	protected virtual ISnapshot CreateTestSnapshot(
		string aggregateId,
		string aggregateType,
		long version,
		byte[] data,
		SnapshotMetadata? metadata = null)
	{
		return new ConformanceSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			aggregateType,
			version,
			DateTimeOffset.UtcNow,
			data,
			metadata != null ? new Dictionary<string, object>
			{
				["LastAppliedEventTimestamp"] = metadata.LastAppliedEventTimestamp,
				["LastAppliedEventId"] = metadata.LastAppliedEventId,
				["SnapshotVersion"] = metadata.SnapshotVersion,
				["SerializerVersion"] = metadata.SerializerVersion
			} : null);
	}

	/// <summary>
	/// Conformance arms this provider has a tracked, documented-pending gap for, keyed by test name
	/// with the provider's own tracking id as the value.
	/// </summary>
	/// <remarks>
	/// Ported from the outbox conformance base, which has carried this for eight arms while this suite
	/// had none. The point is not convenience — it is that SILENCE IS NOT AN OPTION. A provider that
	/// cannot yet satisfy an arm has exactly two honest moves: fail it, or declare it here and be named
	/// in the output alongside its own bead. Neither is quiet. A suite without this mechanism offers a
	/// third move that looks like neither, which is how a gap survives a green build.
	/// </remarks>
	protected virtual IReadOnlyDictionary<string, string> PendingConformanceGaps =>
		new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Skips <paramref name="testName"/> ONLY for a provider that declares it in
	/// <see cref="PendingConformanceGaps"/>, citing that provider's own tracking id. Every
	/// non-declaring provider runs the arm, so the contract stays covered where it is implemented.
	/// </summary>
	/// <param name="testName">The conformance arm's own name (pass <c>nameof(...)</c>).</param>
	private void SkipIfPending(string testName)
	{
		if (PendingConformanceGaps.TryGetValue(testName, out var trackingId))
		{
			Assert.Skip(
				$"pending {trackingId} — {GetType().Name} has a tracked, documented-pending conformance gap " +
				$"for '{testName}' (required contract; NOT a capability-gate).");
		}
	}

	#region Tenant Isolation

	// Two tenants can legitimately hold the SAME aggregate id. Every arm shares one id between two
	// tenants on purpose — that collision is the property under test, not an edge case.
	//
	// ONE store, TWO ambient scopes: the topology production actually runs. See the note above
	// CreateTestSnapshot for why a store-per-tenant seam was removed rather than kept.

	/// <summary>
	/// A higher-versioned save by another tenant MUST NOT overwrite this tenant's snapshot.
	/// </summary>
	[Fact]
	public virtual async Task Should_Not_Let_Another_Tenants_Higher_Version_Overwrite_This_Tenant()
	{
		SkipIfPending(nameof(Should_Not_Let_Another_Tenants_Higher_Version_Overwrite_This_Tenant));

		var sharedAggregateId = Guid.NewGuid().ToString();
		var store = SnapshotStore!;

		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			await store.SaveSnapshotAsync(
				CreateTenantSnapshot(sharedAggregateId, 5, "A-data", "tenant-a"),
				CancellationToken.None).ConfigureAwait(false);
		}

		// B's version is HIGHER. An upsert keyed only on the aggregate matches A's row and updates it.
		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			await store.SaveSnapshotAsync(
				CreateTenantSnapshot(sharedAggregateId, 7, "B-data", "tenant-b"),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? readByA;
		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			readByA = await store.GetLatestSnapshotAsync(
				sharedAggregateId, "TestAggregate", CancellationToken.None).ConfigureAwait(false);
		}

		_ = readByA.ShouldNotBeNull(
			"tenant A's snapshot must still exist after tenant B saved a higher version for the same aggregate id");
		Encoding.UTF8.GetString(readByA.Data.ToArray()).ShouldBe(
			"A-data",
			"tenant A read tenant B's data: a save keyed only on the aggregate OVERWROTE another tenant's row");
		readByA.Version.ShouldBe(
			5,
			"tenant A's version was replaced by tenant B's — the rows were merged rather than kept distinct");
	}

	/// <summary>
	/// A lower-versioned save by another tenant MUST NOT be silently discarded.
	/// </summary>
	[Fact]
	public virtual async Task Should_Not_Silently_Discard_A_Tenants_Save_Behind_Another_Tenants_Version()
	{
		SkipIfPending(nameof(Should_Not_Silently_Discard_A_Tenants_Save_Behind_Another_Tenants_Version));

		var sharedAggregateId = Guid.NewGuid().ToString();
		var store = SnapshotStore!;

		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			await store.SaveSnapshotAsync(
				CreateTenantSnapshot(sharedAggregateId, 5, "A-data", "tenant-a"),
				CancellationToken.None).ConfigureAwait(false);
		}

		// B's version is LOWER. A version-guarded upsert matches A's row, fails the guard, and does
		// nothing — reporting success. This is the nastier direction: the save API returns normally and
		// B's data is simply gone. A round-trip written from A's point of view never sees it.
		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			await store.SaveSnapshotAsync(
				CreateTenantSnapshot(sharedAggregateId, 3, "B-data", "tenant-b"),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? readByB;
		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			readByB = await store.GetLatestSnapshotAsync(
				sharedAggregateId, "TestAggregate", CancellationToken.None).ConfigureAwait(false);
		}

		_ = readByB.ShouldNotBeNull(
			"tenant B's save was reported successful and then returned nothing: a silent write-loss");
		Encoding.UTF8.GetString(readByB.Data.ToArray()).ShouldBe(
			"B-data",
			"tenant B read tenant A's data — B's own save was discarded behind A's higher version");
	}

	/// <summary>
	/// LIVENESS: both tenants' snapshots coexist and each reads its own.
	/// </summary>
	[Fact]
	public virtual async Task Should_Serve_Each_Tenant_Its_Own_Snapshot_For_A_Shared_Aggregate_Id()
	{
		SkipIfPending(nameof(Should_Serve_Each_Tenant_Its_Own_Snapshot_For_A_Shared_Aggregate_Id));

		// Not ceremony. Any fix that scopes reads on a column nothing populates — or fails closed on a
		// null tenant — satisfies BOTH arms above perfectly, by returning nothing to anybody. A store
		// that has stopped working is trivially "isolated".
		var sharedAggregateId = Guid.NewGuid().ToString();
		var store = SnapshotStore!;

		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			await store.SaveSnapshotAsync(
				CreateTenantSnapshot(sharedAggregateId, 5, "A-data", "tenant-a"),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			await store.SaveSnapshotAsync(
				CreateTenantSnapshot(sharedAggregateId, 3, "B-data", "tenant-b"),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? readByA;
		ISnapshot? readByB;
		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			readByA = await store.GetLatestSnapshotAsync(
				sharedAggregateId, "TestAggregate", CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			readByB = await store.GetLatestSnapshotAsync(
				sharedAggregateId, "TestAggregate", CancellationToken.None).ConfigureAwait(false);
		}

		_ = readByA.ShouldNotBeNull("LIVENESS: tenant A must still be served its own snapshot");
		_ = readByB.ShouldNotBeNull("LIVENESS: tenant B must still be served its own snapshot");
		Encoding.UTF8.GetString(readByA.Data.ToArray()).ShouldBe("A-data", "LIVENESS: A must read A");
		Encoding.UTF8.GetString(readByB.Data.ToArray()).ShouldBe("B-data", "LIVENESS: B must read B");
	}

	/// <summary>
	/// Builds a snapshot owned by a specific tenant, sharing an aggregate id with other tenants.
	/// </summary>
	private static ISnapshot CreateTenantSnapshot(
		string aggregateId,
		long version,
		string state,
		string tenantId) =>
		new ConformanceSnapshot(
			SnapshotId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: "TestAggregate",
			Version: version,
			CreatedAt: DateTimeOffset.UtcNow,
			Data: Encoding.UTF8.GetBytes(state),
			Metadata: null,
			TenantId: tenantId);

	#endregion

	#region R26.27 Snapshot Versioning Tests

	/// <summary>
	/// R26.27: Snapshot MUST carry version metadata.
	/// </summary>
	[Fact]
	public virtual async Task Should_Preserve_Snapshot_Version()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		const long version = 42;
		var snapshot = CreateTestSnapshot(
			aggregateId,
			"TestAggregate",
			version,
			new byte[] { 1, 2, 3, 4 });

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull("R26.27: Should retrieve saved snapshot");
		retrieved.Version.ShouldBe(version, "R26.27: Snapshot version must be preserved");
	}

	/// <summary>
	/// R26.27: Snapshot MUST preserve aggregate ID and type.
	/// </summary>
	[Fact]
	public virtual async Task Should_Preserve_Aggregate_Identity()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "Order";
		var snapshot = CreateTestSnapshot(
			aggregateId,
			aggregateType,
			1,
			new byte[] { 1, 2, 3 });

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.AggregateId.ShouldBe(aggregateId, "R26.27: AggregateId must be preserved");
		retrieved.AggregateType.ShouldBe(aggregateType, "R26.27: AggregateType must be preserved");
	}

	/// <summary>
	/// R26.27: Snapshot data MUST be preserved without corruption.
	/// </summary>
	[Fact]
	public virtual async Task Should_Preserve_Snapshot_Data()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
		var snapshot = CreateTestSnapshot(aggregateId, "TestAggregate", 1, data);

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.Data.ToArray().ShouldBe(data, "R26.27: Snapshot data must be preserved without corruption");
	}

	/// <summary>
	/// R26.27: GetLatestSnapshot MUST return the most recent snapshot version.
	/// </summary>
	[Fact]
	public virtual async Task Should_Return_Latest_Snapshot_When_Multiple_Exist()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "TestAggregate";

		var snapshot1 = CreateTestSnapshot(aggregateId, aggregateType, 10, new byte[] { 1 });
		var snapshot2 = CreateTestSnapshot(aggregateId, aggregateType, 50, new byte[] { 2 });
		var snapshot3 = CreateTestSnapshot(aggregateId, aggregateType, 100, new byte[] { 3 });

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot1, CancellationToken.None).ConfigureAwait(false);
		await SnapshotStore.SaveSnapshotAsync(snapshot2, CancellationToken.None).ConfigureAwait(false);
		await SnapshotStore.SaveSnapshotAsync(snapshot3, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(100, "R26.27: Must return the latest snapshot version");
		retrieved.Data.ToArray().ShouldBe(new byte[] { 3 }, "R26.27: Must return the latest snapshot data");
	}

	/// <summary>
	/// R26.27: Return null for non-existent aggregate.
	/// </summary>
	[Fact]
	public virtual async Task Should_Return_Null_For_NonExistent_Aggregate()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid().ToString();

		// Act
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			nonExistentId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		retrieved.ShouldBeNull("R26.27: Should return null for non-existent aggregate");
	}

	#endregion R26.27 Snapshot Versioning Tests

	#region R26.29 Snapshot Strategy Tests

	/// <summary>
	/// R26.29: IntervalSnapshotStrategy SHOULD trigger at configured intervals.
	/// </summary>
	[Fact]
	public virtual void Strategy_Should_Trigger_At_Configured_Interval()
	{
		if (SnapshotStrategy == null)
		{
			return; // Skip if no strategy configured
		}

		// Act/Assert - Below threshold
		var aggregate50 = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate50.Version).Returns(50);
		var shouldNotCreate = SnapshotStrategy.ShouldCreateSnapshot(aggregate50);

		shouldNotCreate.ShouldBeFalse("R26.29: Should not trigger below interval threshold");

		// Act/Assert - At threshold
		var aggregate100 = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate100.Version).Returns(100);
		var shouldCreate = SnapshotStrategy.ShouldCreateSnapshot(aggregate100);

		shouldCreate.ShouldBeTrue("R26.29: Should trigger at interval threshold");
	}

	#endregion R26.29 Snapshot Strategy Tests

	#region Snapshot Delete Tests

	/// <summary>
	/// Snapshot store MUST support deleting snapshots by aggregate.
	/// </summary>
	[Fact]
	public virtual async Task Should_Delete_Snapshots_For_Aggregate()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var snapshot = CreateTestSnapshot(aggregateId, "TestAggregate", 100, new byte[] { 1, 2, 3 });

		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		// Verify it exists
		var exists = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);
		_ = exists.ShouldNotBeNull("Precondition: snapshot should exist");

		// Act
		await SnapshotStore.DeleteSnapshotsAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var deleted = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		deleted.ShouldBeNull("Snapshot should be deleted");
	}

	/// <summary>
	/// Snapshot store MUST support deleting old snapshots by version.
	/// </summary>
	[Fact]
	public virtual async Task Should_Delete_Snapshots_Older_Than_Version()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "TestAggregate";
		var oldSnapshot = CreateTestSnapshot(aggregateId, aggregateType, 50, new byte[] { 1 });
		var newSnapshot = CreateTestSnapshot(aggregateId, aggregateType, 100, new byte[] { 2 });

		await SnapshotStore.SaveSnapshotAsync(oldSnapshot, CancellationToken.None).ConfigureAwait(false);
		await SnapshotStore.SaveSnapshotAsync(newSnapshot, CancellationToken.None).ConfigureAwait(false);

		// Act - Delete snapshots with version < 75 (should delete the v50 snapshot)
		await SnapshotStore.DeleteSnapshotsOlderThanAsync(
			aggregateId,
			aggregateType,
			olderThanVersion: 75,
			CancellationToken.None).ConfigureAwait(false);

		// Assert - Latest snapshot (v100) should still be retrievable
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			CancellationToken.None).ConfigureAwait(false);

		_ = retrieved.ShouldNotBeNull("Latest snapshot should be preserved");
		retrieved.Version.ShouldBe(100, "Latest snapshot version should be preserved");
	}

	#endregion Snapshot Delete Tests

	#region Edge Case Tests

	/// <summary>
	/// Snapshot store MUST handle large data payloads.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Large_Snapshot_Data()
	{
		// MaxSnapshotPayloadBytes is a capability declaration, not an opt-out. The arm still stores and
		// reads back the largest payload the provider supports, so it stays non-vacuous everywhere: a
		// store that truncates, corrupts or silently drops a large payload fails it on every provider.
		// Skipping the arm on a size-capped provider would leave its large-payload path untested;
		// deleting it would lose the coverage on the providers that do carry a megabyte.
		// Arrange - the largest payload this provider actually accepts.
		var aggregateId = Guid.NewGuid().ToString();
		var largeData = new byte[MaxSnapshotPayloadBytes];
		new Random(42).NextBytes(largeData);

		var snapshot = CreateTestSnapshot(aggregateId, "LargeAggregate", 1, largeData);

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"LargeAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull("Should handle large snapshot data");
		retrieved.Data.Length.ShouldBe(largeData.Length, "Large data length should be preserved");
		retrieved.Data.ToArray().ShouldBe(largeData, "Large data content should be preserved");
	}

	/// <summary>
	/// Snapshot store MUST handle empty data payloads.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Empty_Snapshot_Data()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var emptyData = Array.Empty<byte>();
		var snapshot = CreateTestSnapshot(aggregateId, "EmptyAggregate", 1, emptyData);

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"EmptyAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull("Should handle empty snapshot data");
		retrieved.Data.ToArray().ShouldBeEmpty("Empty data should be preserved");
	}

	/// <summary>
	/// Snapshot store MUST isolate snapshots by aggregate type.
	/// </summary>
	[Fact]
	public virtual async Task Should_Isolate_Snapshots_By_Aggregate_Type()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();

		var orderSnapshot = CreateTestSnapshot(aggregateId, "Order", 100, new byte[] { 1 });
		var customerSnapshot = CreateTestSnapshot(aggregateId, "Customer", 50, new byte[] { 2 });

		// Act
		await SnapshotStore.SaveSnapshotAsync(orderSnapshot, CancellationToken.None).ConfigureAwait(false);
		await SnapshotStore.SaveSnapshotAsync(customerSnapshot, CancellationToken.None).ConfigureAwait(false);

		var retrievedOrder = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"Order",
			CancellationToken.None).ConfigureAwait(false);

		var retrievedCustomer = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"Customer",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrievedOrder.ShouldNotBeNull();
		retrievedOrder.Version.ShouldBe(100, "Order snapshot should be isolated");
		retrievedOrder.Data.ShouldBe(new byte[] { 1 });

		_ = retrievedCustomer.ShouldNotBeNull();
		retrievedCustomer.Version.ShouldBe(50, "Customer snapshot should be isolated");
		retrievedCustomer.Data.ShouldBe(new byte[] { 2 });
	}

	/// <summary>
	/// Snapshot store MUST support special characters in aggregate IDs.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Special_Characters_In_AggregateId()
	{
		// Arrange
		var aggregateId = "order-123/customer-456:item-789";
		var snapshot = CreateTestSnapshot(aggregateId, "TestAggregate", 1, new byte[] { 1, 2, 3 });

		// Act
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"TestAggregate",
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = retrieved.ShouldNotBeNull("Should handle special characters in aggregate ID");
		retrieved.AggregateId.ShouldBe(aggregateId);
	}

	#endregion Edge Case Tests

	#region Concurrency Tests

	/// <summary>
	/// Snapshot store MUST handle concurrent writes safely.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Concurrent_Writes()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		const int concurrentWrites = 10;
		var tasks = new List<Task>();

		// Act - Concurrent writes with different versions
		for (int i = 1; i <= concurrentWrites; i++)
		{
			var version = i * 10;
			var snapshot = CreateTestSnapshot(aggregateId, "ConcurrentAggregate", version, new byte[] { (byte)i });
			tasks.Add(SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Should have the highest version
		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			"ConcurrentAggregate",
			CancellationToken.None).ConfigureAwait(false);

		_ = retrieved.ShouldNotBeNull("Should handle concurrent writes");
		retrieved.Version.ShouldBe(100, "Should return the latest version after concurrent writes");
	}

	/// <summary>
	/// Snapshot store MUST handle concurrent reads safely.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Concurrent_Reads()
	{
		// Arrange
		var aggregateId = Guid.NewGuid().ToString();
		var snapshot = CreateTestSnapshot(aggregateId, "ReadAggregate", 100, new byte[] { 1, 2, 3 });
		await SnapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		const int concurrentReads = 10;
		var tasks = new List<Task<ISnapshot?>>();

		// Act - Concurrent reads
		for (int i = 0; i < concurrentReads; i++)
		{
			tasks.Add(SnapshotStore.GetLatestSnapshotAsync(
				aggregateId,
				"ReadAggregate",
				CancellationToken.None).AsTask());
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All reads should succeed with same data
		foreach (var result in results)
		{
			_ = result.ShouldNotBeNull("Concurrent read should succeed");
			result.Version.ShouldBe(100, "All reads should return same version");
		}
	}

	#endregion Concurrency Tests

	/// <summary>
	/// Test snapshot implementation for conformance testing.
	/// </summary>
	/// <remarks>
	/// Named <c>ConformanceSnapshot</c>, not <c>TestSnapshot</c>, deliberately. A public
	/// <c>Excalibur.Testing.Conformance.TestSnapshot</c> also implements <see cref="ISnapshot"/>, and
	/// while this type carried that simple name an unqualified <c>new ConformanceSnapshot { … }</c> inside
	/// this class bound HERE rather than there — silently, with no diagnostic, because both satisfy
	/// the same interface. That is not a hazard worth documenting; it is one worth removing, and a
	/// distinct name removes it at the root. Do not rename this back.
	/// </remarks>
	protected sealed record ConformanceSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId = null) : ISnapshot;

	/// <summary>
	/// Creates the <see cref="ITenantContext"/> a provider fixture must pass to its store for the
	/// tenant-isolation arms to exercise the tenanted path.
	/// </summary>
	/// <returns>A context reading the ambient tenant established by <see cref="TenantContextHolder"/>.</returns>
	/// <remarks>
	/// <para>
	/// Exists because the framework ships <b>no public</b> <see cref="ITenantContext"/> implementation —
	/// both <c>AmbientTenantContext</c> and <c>SingleTenantContext</c> are <c>internal</c>, reachable only
	/// from assemblies on the <c>InternalsVisibleTo</c> list. A provider fixture living outside that list
	/// therefore cannot construct one, even though the store constructors accept the public interface.
	/// Rather than widen production visibility or add friend entries per test assembly — neither of which
	/// a fixture's convenience justifies — this reads the ambient tenant through <see cref="TenantContextHolder"/>,
	/// which is public. Any fixture in any assembly can use it.
	/// </para>
	/// <para>
	/// It implements <see cref="ITenantContext"/> <b>directly</b>, inheriting no first-party base. A fixture
	/// that reached the contract through a framework base would re-test that base rather than the interface,
	/// and would keep passing for an implementation the real providers get wrong.
	/// </para>
	/// </remarks>
	protected static ITenantContext CreateAmbientTenantContext() => new ConformanceTenantContext();

	/// <summary>
	/// A read-only view over the ambient tenant, equivalent to the framework's internal default.
	/// </summary>
	private sealed class ConformanceTenantContext : ITenantContext
	{
		/// <inheritdoc />
		public string? TenantId => TenantContextHolder.Current;

		/// <inheritdoc />
		/// <remarks>
		/// Kept consistent with <see cref="TenantId"/> — true exactly when the id is non-null and
		/// non-empty — because the interface states that as an invariant a subtype may not weaken.
		/// </remarks>
		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
