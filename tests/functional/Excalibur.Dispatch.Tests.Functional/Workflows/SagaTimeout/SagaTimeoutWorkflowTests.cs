// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.SagaTimeout;

/// <summary>
/// Functional tests for saga timeout workflows.
/// Tests timeout scheduling, delivery, cancellation, survival across restarts, and multiple timeout tracking.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 197 - Saga Orchestration Advanced Tests.
/// bd-a6gcg: Saga Timeout Tests (5 tests).
/// </para>
/// <para>
/// These tests verify timeout behavior as specified in saga-enhancements-spec.md TIMEOUT-001 through TIMEOUT-006.
/// Uses in-memory timeout store for testing with configurable time control.
/// </para>
/// </remarks>
[FunctionalTest]
public sealed class SagaTimeoutWorkflowTests : FunctionalTestBase
{
	/// <inheritdoc/>
	protected override TimeSpan TestTimeout => TestTimeouts.Functional;

	/// <summary>
	/// Test 1: Verifies that a scheduled timeout is delivered after the specified delay.
	/// </summary>
	[Fact]
	public async Task Saga_Timeout_Is_Delivered_After_Delay()
	{
		// Arrange
		var timeoutStore = new InMemoryTimeoutStore();
		var saga = new TimeoutAwareSaga(timeoutStore);
		var sagaId = "saga-timeout-001";
		var timeoutDelay = TimeSpan.FromMilliseconds(100);

		// Act - Schedule a timeout
		await RunWithTimeoutAsync(async ct =>
		{
			await saga.StartAsync(sagaId).ConfigureAwait(true);
			await saga.RequestTimeoutAsync<InventoryReservationTimeout>(
				sagaId, timeoutDelay).ConfigureAwait(true);

			// Poll until timeout becomes due rather than using a fixed delay
			await WaitUntilAsync(
				() => timeoutStore.HasDueTimeoutsSync(),
				TimeSpan.FromSeconds(5));

			// Process any pending timeouts
			await saga.ProcessPendingTimeoutsAsync().ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Timeout was delivered
		saga.DeliveredTimeouts.ShouldContain(t => t.SagaId == sagaId);
		saga.DeliveredTimeouts.First(t => t.SagaId == sagaId)
			.TimeoutType.ShouldBe(typeof(InventoryReservationTimeout));
	}

	/// <summary>
	/// Test 2: Verifies that a timeout is cancelled when the saga completes before the delay.
	/// </summary>
	[Fact]
	public async Task Saga_Timeout_Is_Cancelled_When_Saga_Completes()
	{
		// Arrange
		var timeoutStore = new InMemoryTimeoutStore();
		var saga = new TimeoutAwareSaga(timeoutStore);
		var sagaId = "saga-timeout-002";
		var timeoutDelay = TimeSpan.FromMilliseconds(500); // Long delay

		// Act - Schedule timeout then complete saga before it fires
		await RunWithTimeoutAsync(async ct =>
		{
			await saga.StartAsync(sagaId).ConfigureAwait(true);
			await saga.RequestTimeoutAsync<PaymentConfirmationTimeout>(
				sagaId, timeoutDelay).ConfigureAwait(true);

			// Complete the saga immediately (cancels pending timeouts)
			await saga.CompleteAsync(sagaId).ConfigureAwait(true);

			// Intentional: must wait past timeout delay to verify cancelled timeout is not delivered
			await Task.Delay(timeoutDelay + TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

			// Process any pending timeouts
			await saga.ProcessPendingTimeoutsAsync().ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Timeout was NOT delivered (cancelled on completion)
		saga.DeliveredTimeouts.ShouldNotContain(t => t.SagaId == sagaId);

		// Assert - Timeout was marked as cancelled
		var pendingTimeouts = await timeoutStore.GetPendingTimeoutsAsync(sagaId).ConfigureAwait(true);
		pendingTimeouts.ShouldBeEmpty();
	}

	/// <summary>
	/// Test 3: Verifies that timeouts survive a simulated process restart.
	/// </summary>
	[Fact]
	public async Task Saga_Timeout_Survives_Process_Restart()
	{
		// Arrange - Shared persistent store (survives restart)
		var timeoutStore = new InMemoryTimeoutStore();
		var sagaId = "saga-timeout-003";
		var timeoutDelay = TimeSpan.FromMilliseconds(100);

		// Act - Schedule timeout, then simulate restart
		await RunWithTimeoutAsync(async ct =>
		{
			// Before restart
			var saga1 = new TimeoutAwareSaga(timeoutStore);
			await saga1.StartAsync(sagaId).ConfigureAwait(true);
			await saga1.RequestTimeoutAsync<ShipmentConfirmationTimeout>(
				sagaId, timeoutDelay).ConfigureAwait(true);

			// Poll until timeout becomes due rather than using a fixed delay
			await WaitUntilAsync(
				() => timeoutStore.HasDueTimeoutsSync(),
				TimeSpan.FromSeconds(5));

			// After restart - new instance recovers from store
			var saga2 = new TimeoutAwareSaga(timeoutStore);
			await saga2.RecoverAsync(sagaId).ConfigureAwait(true);
			await saga2.ProcessPendingTimeoutsAsync().ConfigureAwait(true);

			// Assert - Timeout was delivered after restart
			saga2.DeliveredTimeouts.ShouldContain(t => t.SagaId == sagaId);
		}).ConfigureAwait(true);
	}

	/// <summary>
	/// Test 4: Verifies that multiple timeouts are tracked independently.
	/// </summary>
	[Fact]
	public async Task Saga_Multiple_Timeouts_Are_Tracked_Independently()
	{
		// Arrange
		var timeoutStore = new InMemoryTimeoutStore();
		var saga = new TimeoutAwareSaga(timeoutStore);
		var sagaId = "saga-timeout-004";

		// Act - Schedule 3 timeouts, cancel 1
		await RunWithTimeoutAsync(async ct =>
		{
			await saga.StartAsync(sagaId).ConfigureAwait(true);

			// Schedule 3 different timeouts
			var timeout1Id = await saga.RequestTimeoutAsync<InventoryReservationTimeout>(
				sagaId, TimeSpan.FromMilliseconds(100)).ConfigureAwait(true);
			var timeout2Id = await saga.RequestTimeoutAsync<PaymentConfirmationTimeout>(
				sagaId, TimeSpan.FromMilliseconds(150)).ConfigureAwait(true);
			var timeout3Id = await saga.RequestTimeoutAsync<ShipmentConfirmationTimeout>(
				sagaId, TimeSpan.FromMilliseconds(200)).ConfigureAwait(true);

			// Cancel timeout 2
			await saga.CancelTimeoutAsync(sagaId, timeout2Id).ConfigureAwait(true);

			// Poll until all non-cancelled timeouts become due
			await WaitUntilAsync(
				() => timeoutStore.GetDueTimeoutCountSync() >= 2,
				TimeSpan.FromSeconds(5));

			// Process pending
			await saga.ProcessPendingTimeoutsAsync().ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Only timeouts 1 and 3 were delivered
		saga.DeliveredTimeouts.Count.ShouldBe(2);
		saga.DeliveredTimeouts.ShouldContain(t => t.TimeoutType == typeof(InventoryReservationTimeout));
		saga.DeliveredTimeouts.ShouldNotContain(t => t.TimeoutType == typeof(PaymentConfirmationTimeout));
		saga.DeliveredTimeouts.ShouldContain(t => t.TimeoutType == typeof(ShipmentConfirmationTimeout));
	}

	/// <summary>
	/// Test 5: Verifies that a timeout can be explicitly cancelled.
	/// </summary>
	[Fact]
	public async Task Saga_Timeout_Can_Be_Explicitly_Cancelled()
	{
		// Arrange
		var timeoutStore = new InMemoryTimeoutStore();
		var saga = new TimeoutAwareSaga(timeoutStore);
		var sagaId = "saga-timeout-005";
		var timeoutDelay = TimeSpan.FromMilliseconds(200);

		// Act - Schedule then cancel before firing
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync(sagaId).ConfigureAwait(true);

			var timeoutId = await saga.RequestTimeoutAsync<InventoryReservationTimeout>(
				sagaId, timeoutDelay).ConfigureAwait(true);

			// Intentional: short wait to ensure timeout is still pending before cancelling
			await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);
			var cancelled = await saga.CancelTimeoutAsync(sagaId, timeoutId).ConfigureAwait(true);

			// Intentional: must wait past original delay to verify cancelled timeout is not delivered
			await Task.Delay(timeoutDelay).ConfigureAwait(true);
			await saga.ProcessPendingTimeoutsAsync().ConfigureAwait(true);

			// Assert inline - cancellation succeeded
			cancelled.ShouldBeTrue();
		}).ConfigureAwait(true);

		// Assert - Timeout was NOT delivered
		saga.DeliveredTimeouts.ShouldBeEmpty();

		// Assert - Timeout is marked cancelled in store
		var timeout = await timeoutStore.GetTimeoutAsync(sagaId, "timeout-0").ConfigureAwait(true);
		_ = timeout.ShouldNotBeNull();
		timeout.Status.ShouldBe(TimeoutStatus.Cancelled);
	}

	#region Test Infrastructure

	/// <summary>
	/// Timeout status.
	/// </summary>
	public enum TimeoutStatus
	{
		Pending,
		Delivered,
		Cancelled,
	}

	/// <summary>
	/// Timeout marker for inventory reservation.
	/// </summary>
	public sealed class InventoryReservationTimeout
	{ }

	/// <summary>
	/// Timeout marker for payment confirmation.
	/// </summary>
	public sealed class PaymentConfirmationTimeout
	{ }

	/// <summary>
	/// Timeout marker for shipment confirmation.
	/// </summary>
	public sealed class ShipmentConfirmationTimeout
	{ }

	/// <summary>
	/// Stored timeout entry.
	/// </summary>
	public sealed class TimeoutEntry
	{
		public string Id { get; init; } = string.Empty;
		public string SagaId { get; init; } = string.Empty;
		public Type TimeoutType { get; init; } = typeof(object);
		public DateTimeOffset DueAt { get; init; }
		public TimeoutStatus Status { get; set; } = TimeoutStatus.Pending;
	}

	/// <summary>
	/// Delivered timeout record.
	/// </summary>
	public sealed record DeliveredTimeout(string SagaId, Type TimeoutType, DateTimeOffset DeliveredAt);

	/// <summary>
	/// In-memory timeout store for testing.
	/// </summary>
	public sealed class InMemoryTimeoutStore
	{
		private readonly ConcurrentDictionary<string, List<TimeoutEntry>> _timeouts = new();

		public Task SaveTimeoutAsync(TimeoutEntry entry)
		{
			var list = _timeouts.GetOrAdd(entry.SagaId, _ => new List<TimeoutEntry>());
			lock (list)
			{
				list.Add(entry);
			}
			return Task.CompletedTask;
		}

		public Task<TimeoutEntry?> GetTimeoutAsync(string sagaId, string timeoutId)
		{
			if (_timeouts.TryGetValue(sagaId, out var list))
			{
				lock (list)
				{
					return Task.FromResult(list.FirstOrDefault(t => t.Id == timeoutId));
				}
			}
			return Task.FromResult<TimeoutEntry?>(null);
		}

		public Task<List<TimeoutEntry>> GetPendingTimeoutsAsync(string sagaId)
		{
			if (_timeouts.TryGetValue(sagaId, out var list))
			{
				lock (list)
				{
					return Task.FromResult(list.Where(t => t.Status == TimeoutStatus.Pending).ToList());
				}
			}
			return Task.FromResult(new List<TimeoutEntry>());
		}

		public Task<List<TimeoutEntry>> GetAllDueTimeoutsAsync()
		{
			var now = DateTimeOffset.UtcNow;
			var due = new List<TimeoutEntry>();
			foreach (var kvp in _timeouts)
			{
				lock (kvp.Value)
				{
					due.AddRange(kvp.Value.Where(t =>
						t.Status == TimeoutStatus.Pending && t.DueAt <= now));
				}
			}
			return Task.FromResult(due);
		}

		public Task UpdateStatusAsync(string sagaId, string timeoutId, TimeoutStatus status)
		{
			if (_timeouts.TryGetValue(sagaId, out var list))
			{
				lock (list)
				{
					var entry = list.FirstOrDefault(t => t.Id == timeoutId);
					if (entry != null)
					{
						entry.Status = status;
					}
				}
			}
			return Task.CompletedTask;
		}

		public bool HasDueTimeoutsSync()
		{
			var now = DateTimeOffset.UtcNow;
			foreach (var kvp in _timeouts)
			{
				lock (kvp.Value)
				{
					if (kvp.Value.Any(t => t.Status == TimeoutStatus.Pending && t.DueAt <= now))
					{
						return true;
					}
				}
			}

			return false;
		}

		public int GetDueTimeoutCountSync()
		{
			var now = DateTimeOffset.UtcNow;
			var count = 0;
			foreach (var kvp in _timeouts)
			{
				lock (kvp.Value)
				{
					count += kvp.Value.Count(t => t.Status == TimeoutStatus.Pending && t.DueAt <= now);
				}
			}

			return count;
		}

		public Task CancelAllForSagaAsync(string sagaId)
		{
			if (_timeouts.TryGetValue(sagaId, out var list))
			{
				lock (list)
				{
					foreach (var entry in list.Where(t => t.Status == TimeoutStatus.Pending))
					{
						entry.Status = TimeoutStatus.Cancelled;
					}
				}
			}
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Saga with timeout support for testing.
	/// </summary>
	public sealed class TimeoutAwareSaga
	{
		private readonly InMemoryTimeoutStore _timeoutStore;
		private readonly ConcurrentDictionary<string, bool> _activeSagas = new();
		private int _timeoutCounter;

		public TimeoutAwareSaga(InMemoryTimeoutStore timeoutStore)
		{
			_timeoutStore = timeoutStore;
		}

		public ConcurrentBag<DeliveredTimeout> DeliveredTimeouts { get; } = new();

		public Task StartAsync(string sagaId)
		{
			_activeSagas[sagaId] = true;
			return Task.CompletedTask;
		}

		public Task RecoverAsync(string sagaId)
		{
			_activeSagas[sagaId] = true;
			return Task.CompletedTask;
		}

		public async Task CompleteAsync(string sagaId)
		{
			_activeSagas[sagaId] = false;
			// Cancel all pending timeouts for this saga
			await _timeoutStore.CancelAllForSagaAsync(sagaId).ConfigureAwait(false);
		}

		public async Task<string> RequestTimeoutAsync<TTimeout>(string sagaId, TimeSpan delay)
			where TTimeout : class
		{
			var timeoutId = $"timeout-{Interlocked.Increment(ref _timeoutCounter) - 1}";
			var entry = new TimeoutEntry
			{
				Id = timeoutId,
				SagaId = sagaId,
				TimeoutType = typeof(TTimeout),
				DueAt = DateTimeOffset.UtcNow + delay,
				Status = TimeoutStatus.Pending,
			};
			await _timeoutStore.SaveTimeoutAsync(entry).ConfigureAwait(false);
			return timeoutId;
		}

		public async Task<bool> CancelTimeoutAsync(string sagaId, string timeoutId)
		{
			var entry = await _timeoutStore.GetTimeoutAsync(sagaId, timeoutId).ConfigureAwait(false);
			if (entry == null || entry.Status != TimeoutStatus.Pending)
			{
				return false;
			}
			await _timeoutStore.UpdateStatusAsync(sagaId, timeoutId, TimeoutStatus.Cancelled)
				.ConfigureAwait(false);
			return true;
		}

		public async Task ProcessPendingTimeoutsAsync()
		{
			var dueTimeouts = await _timeoutStore.GetAllDueTimeoutsAsync().ConfigureAwait(false);
			foreach (var timeout in dueTimeouts)
			{
				// Only deliver if saga is still active
				if (_activeSagas.TryGetValue(timeout.SagaId, out var active) && active)
				{
					await _timeoutStore.UpdateStatusAsync(
						timeout.SagaId, timeout.Id, TimeoutStatus.Delivered).ConfigureAwait(false);
					DeliveredTimeouts.Add(new DeliveredTimeout(
						timeout.SagaId, timeout.TimeoutType, DateTimeOffset.UtcNow));
				}
			}
		}
	}

	#endregion Test Infrastructure
}
