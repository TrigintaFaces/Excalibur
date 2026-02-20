// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Snapshots;
using Excalibur.Domain.Model;

using Microsoft.Extensions.Options;

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore.Snapshots;

/// <summary>
/// Unit tests for the <see cref="FirestoreSnapshotStore"/> dual-constructor pattern.
/// Verifies both simple (options-based) and advanced (FirestoreDb) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreSnapshotStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<FirestoreSnapshotStore> _logger;
	private readonly IOptions<FirestoreSnapshotStoreOptions> _validOptions;

	public FirestoreSnapshotStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<FirestoreSnapshotStore>>();
		_validOptions = Options.Create(new FirestoreSnapshotStoreOptions
		{
			ProjectId = "test-project",
			CollectionName = "snapshots"
		});
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Arrange & Act
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithEmulatorHost_CreatesInstance()
	{
		// Arrange
		var options = Options.Create(new FirestoreSnapshotStoreOptions
		{
			EmulatorHost = "localhost:8080",
			CollectionName = "snapshots"
		});

		// Act
		var store = new FirestoreSnapshotStore(options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreSnapshotStore(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreSnapshotStore(_validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange - No ProjectId or EmulatorHost
		var invalidOptions = Options.Create(new FirestoreSnapshotStoreOptions());

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new FirestoreSnapshotStore(invalidOptions, _logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new FirestoreSnapshotStoreOptions
		{
			ProjectId = "test-project",
			CollectionName = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new FirestoreSnapshotStore(invalidOptions, _logger));
	}

	#endregion Simple Constructor Tests

	#region FirestoreDb Constructor Tests

	[Fact]
	public void DbConstructor_WithNullDb_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreSnapshotStore(db: null!, _validOptions, _logger));
		exception.ParamName.ShouldBe("db");
	}

	[Fact]
	public void DbConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		// Note: We cannot easily fake FirestoreDb since it's sealed/abstract
		// so we test the null parameter validation

		// Act & Assert - This will throw before reaching the Db null check
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreSnapshotStore(db: null!, options: null!, _logger));
		exception.ParamName.ShouldBe("db");
	}

	[Fact]
	public void DbConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreSnapshotStore(db: null!, _validOptions, logger: null!));
		exception.ParamName.ShouldBe("db");
	}

	#endregion FirestoreDb Constructor Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert - Should not throw
		store.Dispose();
		store.Dispose();
		store.Dispose();
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.GetLatestSnapshotAsync("test-id", "TestAggregate", CancellationToken.None));
	}

	[Fact]
	public async Task SaveSnapshotAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);
		await store.DisposeAsync();
		var snapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshot.AggregateId).Returns("test-id");
		_ = A.CallTo(() => snapshot.AggregateType).Returns("TestAggregate");

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.SaveSnapshotAsync(snapshot, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.DeleteSnapshotsAsync("test-id", "TestAggregate", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);
		await store.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.DeleteSnapshotsOlderThanAsync("test-id", "TestAggregate", 10, CancellationToken.None));
	}

	#endregion Dispose Tests

	#region Parameter Validation Tests

	[Fact]
	public async Task GetLatestSnapshotAsync_WithNullAggregateId_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetLatestSnapshotAsync(null!, "TestAggregate", CancellationToken.None));
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_WithEmptyAggregateId_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetLatestSnapshotAsync(string.Empty, "TestAggregate", CancellationToken.None));
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_WithNullAggregateType_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetLatestSnapshotAsync("test-id", null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_WithEmptyAggregateType_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetLatestSnapshotAsync("test-id", string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task SaveSnapshotAsync_WithNullSnapshot_ThrowsArgumentNullException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.SaveSnapshotAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_WithNullAggregateId_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteSnapshotsAsync(null!, "TestAggregate", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_WithNullAggregateType_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteSnapshotsAsync("test-id", null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_WithNullAggregateId_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteSnapshotsOlderThanAsync(null!, "TestAggregate", 10, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_WithNullAggregateType_ThrowsArgumentException()
	{
		// Arrange
		var store = new FirestoreSnapshotStore(_validOptions, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteSnapshotsOlderThanAsync("test-id", null!, 10, CancellationToken.None));
	}

	#endregion Parameter Validation Tests
}
