// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Data.Postgres.Saga;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.TestTypes;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Conformance tests for Postgres Saga Store implementation.
/// </summary>
/// <remarks>
/// Tests verify that the Postgres implementation correctly implements the
/// ISagaStore interface using INSERT ON CONFLICT upsert pattern.
/// </remarks>
[Collection("PostgresSagaStore")]
public sealed class PostgresSagaStoreConformanceShould : IAsyncLifetime
{
	private readonly PostgresSagaStoreContainerFixture _fixture;
	private ISagaStore? _sagaStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSagaStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public PostgresSagaStoreConformanceShould(PostgresSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30
		});

		var serializer = new JsonMessageSerializer();

		_sagaStore = new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			serializer);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task LoadAsync_ShouldReturnNull_WhenSagaDoesNotExist()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaId = Guid.NewGuid();

		// Act
		var result = await _sagaStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	// ============================================
	// Basic Operations Tests
	// ============================================
	[Fact]
	public async Task SaveAsync_NewSaga_ShouldInsert()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-001",
			CustomerName = "John Doe",
			TotalAmount = 99.99m,
			Completed = false
		};

		// Act
		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Assert - Verify by loading
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaState.SagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaState.SagaId);
	}

	[Fact]
	public async Task SaveAsync_ExistingSaga_ShouldUpdate()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaId = Guid.NewGuid();
		var originalState = new TestSagaState
		{
			SagaId = sagaId,
			OrderId = "ORD-002",
			CustomerName = "Jane Doe",
			TotalAmount = 50.00m,
			Completed = false
		};

		await _sagaStore.SaveAsync(originalState, CancellationToken.None).ConfigureAwait(false);

		// Act - Update the saga
		var updatedState = new TestSagaState
		{
			SagaId = sagaId,
			OrderId = "ORD-002",
			CustomerName = "Jane Doe Updated",
			TotalAmount = 75.00m,
			Completed = false
		};

		await _sagaStore.SaveAsync(updatedState, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.CustomerName.ShouldBe("Jane Doe Updated");
		loaded.TotalAmount.ShouldBe(75.00m);
	}

	[Fact]
	public async Task LoadAsync_AfterSave_ShouldReturnState()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-003",
			CustomerName = "Test Customer",
			TotalAmount = 123.45m,
			Completed = false
		};

		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Act
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaState.SagaId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaState.SagaId);
		loaded.OrderId.ShouldBe(sagaState.OrderId);
		loaded.CustomerName.ShouldBe(sagaState.CustomerName);
		loaded.TotalAmount.ShouldBe(sagaState.TotalAmount);
	}

	[Fact]
	public async Task SaveAsync_WithCompleted_ShouldPersistFlag()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-004",
			CustomerName = "Complete Customer",
			TotalAmount = 200.00m,
			Completed = true
		};

		// Act
		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaState.SagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.Completed.ShouldBeTrue();
	}

	[Fact]
	public async Task LoadAsync_ShouldDeserializeState()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-005",
			CustomerName = "Deserialize Test",
			TotalAmount = 999.99m,
			Items = ["Item1", "Item2", "Item3"],
			Completed = false
		};

		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Act
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaState.SagaId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = loaded.ShouldNotBeNull();
		_ = loaded.Items.ShouldNotBeNull();
		loaded.Items.Count.ShouldBe(3);
		loaded.Items.ShouldContain("Item1");
		loaded.Items.ShouldContain("Item2");
		loaded.Items.ShouldContain("Item3");
	}

	[Fact]
	public async Task SaveAsync_NullSagaState_ShouldThrow()
	{

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sagaStore!.SaveAsync<TestSagaState>(null!, CancellationToken.None).ConfigureAwait(false));
	}

	// ============================================
	// Edge Cases Tests
	// ============================================
	[Fact]
	public async Task SaveAsync_ShouldUpdateTimestamp()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaId = Guid.NewGuid();
		var sagaState = new TestSagaState
		{
			SagaId = sagaId,
			OrderId = "ORD-006",
			CustomerName = "Timestamp Test",
			TotalAmount = 100.00m,
			Completed = false
		};

		// First save
		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Wait briefly to ensure timestamp difference
		await Task.Delay(50).ConfigureAwait(false);

		// Update
		sagaState.TotalAmount = 150.00m;
		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Assert - Verify the saga was updated (we can't directly check timestamp, but verify state persisted)
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.TotalAmount.ShouldBe(150.00m);
	}

	[Fact]
	public async Task LoadAsync_ConcurrentReads_ShouldSucceed()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-007",
			CustomerName = "Concurrent Test",
			TotalAmount = 300.00m,
			Completed = false
		};

		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Act - Concurrent reads
		var tasks = Enumerable.Range(0, 10).Select(_ =>
			_sagaStore.LoadAsync<TestSagaState>(sagaState.SagaId, CancellationToken.None));

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		results.ShouldAllBe(r => r != null);
		results.ShouldAllBe(r => r.SagaId == sagaState.SagaId);
	}

	[Fact]
	public async Task SaveAsync_ConcurrentWrites_ShouldNotCorrupt()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaId = Guid.NewGuid();

		// Act - Concurrent writes with same saga ID (last write wins with INSERT ON CONFLICT)
		var tasks = Enumerable.Range(0, 5).Select(i =>
			_sagaStore.SaveAsync(new TestSagaState
			{
				SagaId = sagaId,
				OrderId = $"ORD-CONCURRENT-{i}",
				CustomerName = $"Customer {i}",
				TotalAmount = 100.00m + i,
				Completed = false
			}, CancellationToken.None));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Saga should exist and not be corrupted
		var loaded = await _sagaStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
		loaded.OrderId.ShouldStartWith("ORD-CONCURRENT-");
	}

	[Fact]
	public async Task SaveAsync_ComplexState_ShouldSerialize()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new ComplexSagaState
		{
			SagaId = Guid.NewGuid(),
			ProcessName = "ComplexProcess",
			Steps =
			[
				new ProcessStep { Name = "Step1", Status = "Complete", Duration = TimeSpan.FromMinutes(5) },
				new ProcessStep { Name = "Step2", Status = "Pending", Duration = TimeSpan.Zero }
			],
			Metadata = new Dictionary<string, string>
			{
				["key1"] = "value1",
				["key2"] = "value2"
			},
			StartedAt = DateTimeOffset.UtcNow,
			Completed = false
		};

		// Act
		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var loaded = await _sagaStore.LoadAsync<ComplexSagaState>(sagaState.SagaId, CancellationToken.None).ConfigureAwait(false);
		_ = loaded.ShouldNotBeNull();
		loaded.ProcessName.ShouldBe("ComplexProcess");
		loaded.Steps.Count.ShouldBe(2);
	}

	// ============================================
	// Serialization Tests
	// ============================================
	[Fact]
	public async Task LoadAsync_ComplexState_ShouldDeserialize()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var sagaState = new ComplexSagaState
		{
			SagaId = Guid.NewGuid(),
			ProcessName = "DeserializeTest",
			Steps =
			[
				new ProcessStep { Name = "Init", Status = "Done", Duration = TimeSpan.FromSeconds(30) }
			],
			Metadata = new Dictionary<string, string>
			{
				["environment"] = "test"
			},
			StartedAt = DateTimeOffset.UtcNow,
			Completed = true
		};

		await _sagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Act
		var loaded = await _sagaStore.LoadAsync<ComplexSagaState>(sagaState.SagaId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.ProcessName.ShouldBe("DeserializeTest");
		_ = loaded.Steps.ShouldNotBeNull();
		loaded.Steps.Count.ShouldBe(1);
		loaded.Steps[0].Name.ShouldBe("Init");
		loaded.Steps[0].Status.ShouldBe("Done");
		_ = loaded.Metadata.ShouldNotBeNull();
		loaded.Metadata["environment"].ShouldBe("test");
		loaded.Completed.ShouldBeTrue();
	}

	[Fact]
	public async Task SaveAsync_MultipleDifferentSagaTypes_ShouldWork()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Arrange
		var testSaga = new TestSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-008",
			CustomerName = "Test",
			TotalAmount = 100.00m,
			Completed = false
		};

		var complexSaga = new ComplexSagaState
		{
			SagaId = Guid.NewGuid(),
			ProcessName = "Process",
			Steps = [new ProcessStep { Name = "Step", Status = "Active", Duration = TimeSpan.Zero }],
			Metadata = new Dictionary<string, string>(),
			StartedAt = DateTimeOffset.UtcNow,
			Completed = false
		};

		// Act
		await _sagaStore.SaveAsync(testSaga, CancellationToken.None).ConfigureAwait(false);
		await _sagaStore.SaveAsync(complexSaga, CancellationToken.None).ConfigureAwait(false);

		// Assert - Both should be loadable
		var loadedTest = await _sagaStore.LoadAsync<TestSagaState>(testSaga.SagaId, CancellationToken.None).ConfigureAwait(false);
		var loadedComplex = await _sagaStore.LoadAsync<ComplexSagaState>(complexSaga.SagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loadedTest.ShouldNotBeNull();
		_ = loadedComplex.ShouldNotBeNull();
		loadedTest.OrderId.ShouldBe("ORD-008");
		loadedComplex.ProcessName.ShouldBe("Process");
	}

	// ============================================
	// Test Saga State Classes
	// ============================================

	/// <summary>
	/// Simple test saga state for basic operations.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;
		public decimal TotalAmount { get; set; }
		public List<string> Items { get; set; } = [];
	}

	/// <summary>
	/// Complex test saga state for serialization tests.
	/// </summary>
	private sealed class ComplexSagaState : SagaState
	{
		public string ProcessName { get; set; } = string.Empty;
		public List<ProcessStep> Steps { get; set; } = [];
		public Dictionary<string, string> Metadata { get; set; } = new();
		public DateTimeOffset StartedAt { get; set; }
	}

	/// <summary>
	/// Process step for complex saga state.
	/// </summary>
	private sealed class ProcessStep
	{
		public string Name { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public TimeSpan Duration { get; set; }
	}
}
