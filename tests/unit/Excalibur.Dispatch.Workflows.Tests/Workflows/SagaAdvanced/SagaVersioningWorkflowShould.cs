// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.SagaAdvanced;

/// <summary>
/// Saga Versioning workflow tests.
/// Tests version migration, backward/forward compatibility, metadata, and conflict handling.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 183 - Functional Testing Epic Phase 3.
/// bd-e4xje: Saga Versioning Tests (5 tests).
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "183")]
[Trait("Component", "SagaAdvanced")]
[Trait("Category", "Unit")]
public sealed class SagaVersioningWorkflowShould
{
	/// <summary>
	/// Tests that saga state migrates from v1 to v2 schema.
	/// Old saga version > Upgrade > New version runs.
	/// </summary>
	[Fact]
	public async Task MigrateSagaFromV1ToV2Schema()
	{
		// Arrange - Create a v1 saga state
		var store = new VersionedSagaStore();
		var migrator = new SagaMigrator(store);
		var log = new ExecutionLog();

		var v1State = new V1SagaState
		{
			SagaId = "saga-v1",
			OrderId = "ORD-V1",
			Amount = 100m,
			Status = "InProgress",
			CurrentStep = 1,
		};
		await store.SaveV1Async(v1State).ConfigureAwait(true);

		// Act - Migrate to v2
		var migratedState = await migrator.MigrateToV2Async("saga-v1").ConfigureAwait(true);

		// Assert - V2 schema populated correctly
		_ = migratedState.ShouldNotBeNull();
		migratedState.SagaId.ShouldBe("saga-v1");
		migratedState.Version.ShouldBe("2.0");
		migratedState.Data.OrderId.ShouldBe("ORD-V1");
		migratedState.Data.Amount.ShouldBe(100m);
		migratedState.Status.ShouldBe(SagaStatus.InProgress);
		migratedState.CurrentStep.ShouldBe(SagaStep.ReserveInventory); // Mapped from step 1

		// Assert - Original v1 state preserved in metadata
		migratedState.Metadata.ShouldContainKey("MigratedFromVersion");
		migratedState.Metadata["MigratedFromVersion"].ShouldBe("1.0");
		migratedState.Metadata.ShouldContainKey("MigrationTimestamp");
	}

	/// <summary>
	/// Tests that v2 saga can read v1 state (backward compatibility).
	/// V2 runtime > Load V1 state > Works.
	/// </summary>
	[Fact]
	public async Task LoadV1StateFromV2Runtime()
	{
		// Arrange - V1 state in store
		var store = new VersionedSagaStore();
		var v1State = new V1SagaState
		{
			SagaId = "saga-backward",
			OrderId = "ORD-BACK",
			Amount = 250m,
			Status = "Pending",
			CurrentStep = 0,
		};
		await store.SaveV1Async(v1State).ConfigureAwait(true);

		// Act - V2 runtime loads state
		var v2Loader = new V2SagaLoader(store);
		var loadedState = await v2Loader.LoadAsync("saga-backward").ConfigureAwait(true);

		// Assert - V2 runtime can use V1 state
		_ = loadedState.ShouldNotBeNull();
		loadedState.SagaId.ShouldBe("saga-backward");
		loadedState.Data.OrderId.ShouldBe("ORD-BACK");
		loadedState.Data.Amount.ShouldBe(250m);
		loadedState.Status.ShouldBe(SagaStatus.Pending);
		loadedState.IsLegacyVersion.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that v1 runtime gracefully handles unknown v2 fields (forward compatibility).
	/// V1 runtime > Load V2 state with new fields > Ignores unknowns.
	/// </summary>
	[Fact]
	public async Task HandleUnknownV2FieldsInV1Runtime()
	{
		// Arrange - V2 state with new fields
		var store = new VersionedSagaStore();
		var v2State = new V2SagaState
		{
			SagaId = "saga-forward",
			Version = "2.0",
			Status = SagaStatus.InProgress,
			CurrentStep = SagaStep.ProcessPayment,
			Data = new OrderData
			{
				OrderId = "ORD-FORWARD",
				Amount = 500m,
				CustomerId = "CUST-FWD", // V2 field
				Notes = "V2 only field", // V2 field
			},
			Priority = SagaPriority.High, // V2 field
		};
		await store.SaveV2Async(v2State).ConfigureAwait(true);

		// Act - V1-compatible loader reads state
		var v1Loader = new V1CompatibleLoader(store);
		var loadedState = await v1Loader.LoadAsync("saga-forward").ConfigureAwait(true);

		// Assert - Known fields loaded, unknown fields ignored
		_ = loadedState.ShouldNotBeNull();
		loadedState.SagaId.ShouldBe("saga-forward");
		loadedState.OrderId.ShouldBe("ORD-FORWARD");
		loadedState.Amount.ShouldBe(500m);
		loadedState.Status.ShouldBe("InProgress");
		loadedState.CurrentStep.ShouldBe(2); // ProcessPayment = 2
		loadedState.HasUnknownFields.ShouldBeTrue();
		loadedState.UnknownFieldCount.ShouldBe(3); // CustomerId, Notes, Priority
	}

	/// <summary>
	/// Tests that version metadata is tracked correctly during saga lifecycle.
	/// Create > Execute > Complete > Version history recorded.
	/// </summary>
	[Fact]
	public async Task TrackVersionMetadataThroughLifecycle()
	{
		// Arrange
		var store = new VersionedSagaStore();
		var log = new ExecutionLog();
		var saga = new VersionedSaga(store, log, version: "2.1");

		// Act - Execute full lifecycle
		await saga.StartAsync("saga-meta", new OrderData { OrderId = "ORD-META" }).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-meta", SagaStep.ReserveInventory).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-meta", SagaStep.ProcessPayment).ConfigureAwait(true);
		await saga.ProcessStepAsync("saga-meta", SagaStep.ShipOrder).ConfigureAwait(true);

		// Assert - Version metadata recorded
		var state = await store.GetV2Async("saga-meta").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.Version.ShouldBe("2.1");
		state.Metadata.ShouldContainKey("CreatedWithVersion");
		state.Metadata["CreatedWithVersion"].ShouldBe("2.1");
		state.Metadata.ShouldContainKey("LastModifiedWithVersion");
		state.Metadata["LastModifiedWithVersion"].ShouldBe("2.1");
		state.Metadata.ShouldContainKey("VersionHistory");

		// Version history tracked
		var history = state.Metadata["VersionHistory"] as List<string>;
		_ = history.ShouldNotBeNull();
		history.Count.ShouldBeGreaterThanOrEqualTo(1);
		history.ShouldContain("2.1");
	}

	/// <summary>
	/// Tests that concurrent version modifications are detected and rejected.
	/// Two runtimes modify same saga > Conflict detected.
	/// </summary>
	[Fact]
	public async Task DetectConcurrentVersionConflict()
	{
		// Arrange - Create initial state
		var store = new VersionedSagaStore();
		var log1 = new ExecutionLog();
		var log2 = new ExecutionLog();
		var saga1 = new VersionedSaga(store, log1, version: "2.0");
		var saga2 = new VersionedSaga(store, log2, version: "2.0");

		await saga1.StartAsync("saga-conflict", new OrderData { OrderId = "ORD-CONFLICT" }).ConfigureAwait(true);
		await saga1.ProcessStepAsync("saga-conflict", SagaStep.ReserveInventory).ConfigureAwait(true);

		// Act - Simulate second instance reading same saga
		await saga2.LoadAsync("saga-conflict").ConfigureAwait(true);

		// Both try to update - saga1 succeeds first
		await saga1.ProcessStepAsync("saga-conflict", SagaStep.ProcessPayment).ConfigureAwait(true);

		// saga2 tries to update with stale etag
		var exception = await Should.ThrowAsync<ConcurrencyException>(
			async () => await saga2.ProcessStepWithConflictCheckAsync("saga-conflict", SagaStep.ProcessPayment).ConfigureAwait(true)).ConfigureAwait(true);

		// Assert - Conflict detected
		_ = exception.ShouldNotBeNull();
		exception.Message.ShouldContain("Concurrency conflict");
		exception.ExpectedETag.ShouldNotBe(exception.ActualETag);

		// Assert - Only saga1 changes persisted
		var state = await store.GetV2Async("saga-conflict").ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.CompletedSteps.Count.ShouldBe(2);
		state.CompletedSteps.ShouldContain(SagaStep.ReserveInventory);
		state.CompletedSteps.ShouldContain(SagaStep.ProcessPayment);
	}

	#region Test Infrastructure

	internal enum SagaStatus
	{
		Pending,
		InProgress,
		Completed,
		Failed,
	}

	internal enum SagaStep
	{
		None = 0,
		ReserveInventory = 1,
		ProcessPayment = 2,
		ShipOrder = 3,
	}

	internal enum SagaPriority
	{
		Low,
		Normal,
		High,
	}

	internal sealed class ExecutionLog
	{
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step)
		{
			Steps.Add(step);
		}
	}

	internal sealed class OrderData
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public string Notes { get; init; } = string.Empty;
	}

	// V1 schema
	internal sealed class V1SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public string OrderId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public string Status { get; init; } = "Pending";
		public int CurrentStep { get; init; }
	}

	// V2 schema with new fields
	internal sealed class V2SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public string Version { get; set; } = "2.0";
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public SagaStep CurrentStep { get; set; } = SagaStep.None;
		public List<SagaStep> CompletedSteps { get; } = [];
		public OrderData Data { get; init; } = new();
		public SagaPriority Priority { get; init; } = SagaPriority.Normal;
		public Dictionary<string, object> Metadata { get; } = [];
		public string ETag { get; set; } = Guid.NewGuid().ToString();
		public bool IsLegacyVersion { get; set; }
	}

	// V1-compatible result (subset of V2)
	internal sealed class V1CompatibleResult
	{
		public string SagaId { get; init; } = string.Empty;
		public string OrderId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public string Status { get; init; } = "Pending";
		public int CurrentStep { get; init; }
		public bool HasUnknownFields { get; set; }
		public int UnknownFieldCount { get; set; }
	}

	internal sealed class VersionedSagaStore
	{
		private readonly ConcurrentDictionary<string, V1SagaState> _v1Sagas = new();
		private readonly ConcurrentDictionary<string, V2SagaState> _v2Sagas = new();

		public Task SaveV1Async(V1SagaState state)
		{
			_v1Sagas[state.SagaId] = state;
			return Task.CompletedTask;
		}

		public Task<V1SagaState?> GetV1Async(string sagaId)
		{
			_ = _v1Sagas.TryGetValue(sagaId, out var state);
			return Task.FromResult(state);
		}

		public Task SaveV2Async(V2SagaState state)
		{
			state.ETag = Guid.NewGuid().ToString();
			_v2Sagas[state.SagaId] = state;
			return Task.CompletedTask;
		}

		public Task<V2SagaState?> GetV2Async(string sagaId)
		{
			_ = _v2Sagas.TryGetValue(sagaId, out var state);
			return Task.FromResult(state);
		}

		public Task<bool> TrySaveV2WithETagAsync(V2SagaState state, string expectedETag)
		{
			if (_v2Sagas.TryGetValue(state.SagaId, out var existing))
			{
				if (existing.ETag != expectedETag)
				{
					return Task.FromResult(false);
				}
			}

			state.ETag = Guid.NewGuid().ToString();
			_v2Sagas[state.SagaId] = state;
			return Task.FromResult(true);
		}

		public bool HasV1(string sagaId) => _v1Sagas.ContainsKey(sagaId);

		public bool HasV2(string sagaId) => _v2Sagas.ContainsKey(sagaId);
	}

	internal sealed class SagaMigrator
	{
		private readonly VersionedSagaStore _store;

		public SagaMigrator(VersionedSagaStore store)
		{
			_store = store;
		}

		public async Task<V2SagaState?> MigrateToV2Async(string sagaId)
		{
			var v1State = await _store.GetV1Async(sagaId).ConfigureAwait(false);
			if (v1State == null)
			{
				return null;
			}

			var v2State = new V2SagaState
			{
				SagaId = v1State.SagaId,
				Version = "2.0",
				Status = MapStatus(v1State.Status),
				CurrentStep = MapStep(v1State.CurrentStep),
				Data = new OrderData
				{
					OrderId = v1State.OrderId,
					Amount = v1State.Amount,
				},
			};

			v2State.Metadata["MigratedFromVersion"] = "1.0";
			v2State.Metadata["MigrationTimestamp"] = DateTimeOffset.UtcNow.ToString("O");

			await _store.SaveV2Async(v2State).ConfigureAwait(false);
			return v2State;
		}

		private static SagaStatus MapStatus(string v1Status) =>
			v1Status switch
			{
				"Pending" => SagaStatus.Pending,
				"InProgress" => SagaStatus.InProgress,
				"Completed" => SagaStatus.Completed,
				"Failed" => SagaStatus.Failed,
				_ => SagaStatus.Pending,
			};

		private static SagaStep MapStep(int step) =>
			step switch
			{
				0 => SagaStep.None,
				1 => SagaStep.ReserveInventory,
				2 => SagaStep.ProcessPayment,
				3 => SagaStep.ShipOrder,
				_ => SagaStep.None,
			};
	}

	internal sealed class V2SagaLoader
	{
		private readonly VersionedSagaStore _store;

		public V2SagaLoader(VersionedSagaStore store)
		{
			_store = store;
		}

		public async Task<V2SagaState?> LoadAsync(string sagaId)
		{
			// Try V2 first
			var v2State = await _store.GetV2Async(sagaId).ConfigureAwait(false);
			if (v2State != null)
			{
				return v2State;
			}

			// Fall back to V1 and convert
			var v1State = await _store.GetV1Async(sagaId).ConfigureAwait(false);
			if (v1State == null)
			{
				return null;
			}

			return new V2SagaState
			{
				SagaId = v1State.SagaId,
				Version = "1.0",
				Status = MapStatus(v1State.Status),
				CurrentStep = MapStep(v1State.CurrentStep),
				Data = new OrderData
				{
					OrderId = v1State.OrderId,
					Amount = v1State.Amount,
				},
				IsLegacyVersion = true,
			};
		}

		private static SagaStatus MapStatus(string v1Status) =>
			v1Status switch
			{
				"Pending" => SagaStatus.Pending,
				"InProgress" => SagaStatus.InProgress,
				"Completed" => SagaStatus.Completed,
				"Failed" => SagaStatus.Failed,
				_ => SagaStatus.Pending,
			};

		private static SagaStep MapStep(int step) =>
			step switch
			{
				0 => SagaStep.None,
				1 => SagaStep.ReserveInventory,
				2 => SagaStep.ProcessPayment,
				3 => SagaStep.ShipOrder,
				_ => SagaStep.None,
			};
	}

	internal sealed class V1CompatibleLoader
	{
		private readonly VersionedSagaStore _store;

		public V1CompatibleLoader(VersionedSagaStore store)
		{
			_store = store;
		}

		public async Task<V1CompatibleResult?> LoadAsync(string sagaId)
		{
			var v2State = await _store.GetV2Async(sagaId).ConfigureAwait(false);
			if (v2State != null)
			{
				var unknownFields = 0;
				if (!string.IsNullOrEmpty(v2State.Data.CustomerId))
				{
					unknownFields++;
				}

				if (!string.IsNullOrEmpty(v2State.Data.Notes))
				{
					unknownFields++;
				}

				if (v2State.Priority != SagaPriority.Normal)
				{
					unknownFields++;
				}

				return new V1CompatibleResult
				{
					SagaId = v2State.SagaId,
					OrderId = v2State.Data.OrderId,
					Amount = v2State.Data.Amount,
					Status = v2State.Status.ToString(),
					CurrentStep = (int)v2State.CurrentStep,
					HasUnknownFields = unknownFields > 0,
					UnknownFieldCount = unknownFields,
				};
			}

			var v1State = await _store.GetV1Async(sagaId).ConfigureAwait(false);
			if (v1State == null)
			{
				return null;
			}

			return new V1CompatibleResult
			{
				SagaId = v1State.SagaId,
				OrderId = v1State.OrderId,
				Amount = v1State.Amount,
				Status = v1State.Status,
				CurrentStep = v1State.CurrentStep,
				HasUnknownFields = false,
				UnknownFieldCount = 0,
			};
		}
	}

	internal sealed class ConcurrencyException : Exception
	{
		public ConcurrencyException(string expectedETag, string actualETag)
			: base($"Concurrency conflict detected. Expected ETag: {expectedETag}, Actual: {actualETag}")
		{
			ExpectedETag = expectedETag;
			ActualETag = actualETag;
		}

		public string ExpectedETag { get; }
		public string ActualETag { get; }
	}

	internal sealed class VersionedSaga
	{
		private readonly VersionedSagaStore _store;
		private readonly ExecutionLog _log;
		private readonly string _version;
		private string? _loadedETag;

		public VersionedSaga(VersionedSagaStore store, ExecutionLog log, string version = "2.0")
		{
			_store = store;
			_log = log;
			_version = version;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new V2SagaState
			{
				SagaId = sagaId,
				Version = _version,
				Status = SagaStatus.Pending,
				Data = data,
			};

			state.Metadata["CreatedWithVersion"] = _version;
			state.Metadata["LastModifiedWithVersion"] = _version;
			state.Metadata["VersionHistory"] = new List<string> { _version };

			await _store.SaveV2Async(state).ConfigureAwait(false);
			_log.Log($"Saga:Start:{sagaId}:v{_version}");
		}

		public async Task LoadAsync(string sagaId)
		{
			var state = await _store.GetV2Async(sagaId).ConfigureAwait(false);
			_loadedETag = state?.ETag;
		}

		public async Task ProcessStepAsync(string sagaId, SagaStep step)
		{
			var state = await _store.GetV2Async(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			state.CurrentStep = step;
			state.CompletedSteps.Add(step);
			state.Metadata["LastModifiedWithVersion"] = _version;

			if (state.Metadata.TryGetValue("VersionHistory", out var historyObj) && historyObj is List<string> history)
			{
				if (!history.Contains(_version))
				{
					history.Add(_version);
				}
			}

			if (step == SagaStep.ShipOrder)
			{
				state.Status = SagaStatus.Completed;
			}

			await _store.SaveV2Async(state).ConfigureAwait(false);
			_log.Log($"{step}:Execute:v{_version}");
		}

		public async Task ProcessStepWithConflictCheckAsync(string sagaId, SagaStep step)
		{
			if (string.IsNullOrEmpty(_loadedETag))
			{
				throw new InvalidOperationException("Must call LoadAsync before ProcessStepWithConflictCheckAsync");
			}

			var currentState = await _store.GetV2Async(sagaId).ConfigureAwait(false);
			if (currentState == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			if (currentState.ETag != _loadedETag)
			{
				throw new ConcurrencyException(_loadedETag, currentState.ETag);
			}

			currentState.Status = SagaStatus.InProgress;
			currentState.CurrentStep = step;
			currentState.CompletedSteps.Add(step);
			currentState.Metadata["LastModifiedWithVersion"] = _version;

			var success = await _store.TrySaveV2WithETagAsync(currentState, _loadedETag).ConfigureAwait(false);
			if (!success)
			{
				var actualState = await _store.GetV2Async(sagaId).ConfigureAwait(false);
				throw new ConcurrencyException(_loadedETag, actualState?.ETag ?? "unknown");
			}

			_log.Log($"{step}:Execute:v{_version}");
		}
	}

	#endregion Test Infrastructure
}
