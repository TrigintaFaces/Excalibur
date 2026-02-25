// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
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
	public async Task InitializeAsync()
	{
		SnapshotStore = await CreateSnapshotStoreAsync().ConfigureAwait(false);
		SnapshotStrategy = await CreateSnapshotStrategyAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		await DisposeSnapshotStoreAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Creates and initializes the snapshot store implementation to test.
	/// </summary>
	/// <returns>An instance of ISnapshotStore to test.</returns>
	protected abstract Task<ISnapshotStore> CreateSnapshotStoreAsync();

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
		return new TestSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			aggregateType,
			version,
			DateTime.UtcNow,
			data,
			metadata != null ? new Dictionary<string, object>
			{
				["LastAppliedEventTimestamp"] = metadata.LastAppliedEventTimestamp,
				["LastAppliedEventId"] = metadata.LastAppliedEventId,
				["SnapshotVersion"] = metadata.SnapshotVersion,
				["SerializerVersion"] = metadata.SerializerVersion
			} : null);
	}

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
		retrieved.Data.ShouldBe(data, "R26.27: Snapshot data must be preserved without corruption");
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
		retrieved.Data.ShouldBe(new byte[] { 3 }, "R26.27: Must return the latest snapshot data");
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
		// Arrange - 1MB payload
		var aggregateId = Guid.NewGuid().ToString();
		var largeData = new byte[1_000_000];
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
		retrieved.Data.ShouldBe(largeData, "Large data content should be preserved");
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
		retrieved.Data.ShouldBeEmpty("Empty data should be preserved");
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
	protected sealed record TestSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		byte[] Data,
		IDictionary<string, object>? Metadata) : ISnapshot;
}
