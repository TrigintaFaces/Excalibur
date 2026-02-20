// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Storage;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

using SagaState = Excalibur.Dispatch.Abstractions.Messaging.SagaState;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Timeouts;

/// <summary>
/// Unit tests for Saga&lt;TSagaState&gt; timeout methods including
/// RequestTimeoutAsync, CancelTimeoutAsync, and MarkCompletedAsync.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 215 - Saga Timeouts Foundation.
/// Task: n2y3k (SAGA-013: Unit Tests - 12 tests).
/// </para>
/// <para>
/// Tests use property injection to set the TimeoutStore on the saga,
/// following the Option B design from SoftwareArchitect (AD-3).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Sprint", "215")]
public sealed class SagaTimeoutMethodsShould
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger _logger;
	private readonly InMemorySagaTimeoutStore _timeoutStore;

	public SagaTimeoutMethodsShould()
	{
		_dispatcher = A.Fake<IDispatcher>();
		_logger = NullLogger.Instance;
		_timeoutStore = new InMemorySagaTimeoutStore();
	}

	/// <summary>
	/// Tests that RequestTimeoutAsync schedules a timeout in the store.
	/// </summary>
	[Fact]
	public async Task ScheduleTimeoutWhenRequestTimeoutAsyncCalled()
	{
		// Arrange
		var saga = CreateSagaWithTimeoutStore();

		// Act
		var timeoutId = await saga.TestRequestTimeoutAsync<PaymentTimeout>(
			TimeSpan.FromMinutes(5),
			CancellationToken.None).ConfigureAwait(true);

		// Assert
		timeoutId.ShouldNotBeNullOrWhiteSpace();
		_timeoutStore.GetPendingCount().ShouldBe(1);

		// Verify timeout was scheduled with correct properties
		var dueTimeouts = await _timeoutStore.GetDueTimeoutsAsync(
			DateTime.UtcNow.AddMinutes(10),
			CancellationToken.None).ConfigureAwait(true);

		dueTimeouts.Count.ShouldBe(1);
		dueTimeouts[0].SagaId.ShouldBe(saga.Id.ToString());
		dueTimeouts[0].TimeoutType.ShouldContain(nameof(PaymentTimeout));
	}

	/// <summary>
	/// Tests that RequestTimeoutAsync with typed data includes serialized data.
	/// </summary>
	[Fact]
	public async Task ScheduleTimeoutWithDataWhenProvidingTimeoutData()
	{
		// Arrange
		var saga = CreateSagaWithTimeoutStore();
		var timeoutData = new PaymentTimeout { PaymentId = "PAY-001", Amount = 100.00m };

		// Act
		var timeoutId = await saga.TestRequestTimeoutAsync(
			TimeSpan.FromMinutes(5),
			timeoutData,
			CancellationToken.None).ConfigureAwait(true);

		// Assert
		timeoutId.ShouldNotBeNullOrWhiteSpace();

		var dueTimeouts = await _timeoutStore.GetDueTimeoutsAsync(
			DateTime.UtcNow.AddMinutes(10),
			CancellationToken.None).ConfigureAwait(true);

		dueTimeouts.Count.ShouldBe(1);
		_ = dueTimeouts[0].TimeoutData.ShouldNotBeNull();
		dueTimeouts[0].TimeoutData.Length.ShouldBeGreaterThan(0);
	}

	/// <summary>
	/// Tests that CancelTimeoutAsync removes a scheduled timeout.
	/// </summary>
	[Fact]
	public async Task CancelTimeoutWhenCancelTimeoutAsyncCalled()
	{
		// Arrange
		var saga = CreateSagaWithTimeoutStore();
		var timeoutId = await saga.TestRequestTimeoutAsync<PaymentTimeout>(
			TimeSpan.FromMinutes(5),
			CancellationToken.None).ConfigureAwait(true);

		_timeoutStore.GetPendingCount().ShouldBe(1);

		// Act
		await saga.TestCancelTimeoutAsync(timeoutId, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_timeoutStore.GetPendingCount().ShouldBe(0);
	}

	/// <summary>
	/// Tests that MarkCompletedAsync cancels all pending timeouts.
	/// </summary>
	[Fact]
	public async Task CancelAllTimeoutsWhenMarkCompletedAsyncCalled()
	{
		// Arrange
		var saga = CreateSagaWithTimeoutStore();

		// Schedule multiple timeouts
		_ = await saga.TestRequestTimeoutAsync<PaymentTimeout>(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(true);
		_ = await saga.TestRequestTimeoutAsync<ShippingTimeout>(TimeSpan.FromMinutes(10), CancellationToken.None).ConfigureAwait(true);
		_ = await saga.TestRequestTimeoutAsync<PaymentTimeout>(TimeSpan.FromMinutes(15), CancellationToken.None).ConfigureAwait(true);

		_timeoutStore.GetPendingCount().ShouldBe(3);

		// Act
		await saga.TestMarkCompletedAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		_timeoutStore.GetPendingCount().ShouldBe(0);
		saga.IsCompleted.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that multiple timeouts can be tracked independently.
	/// </summary>
	[Fact]
	public async Task TrackMultipleConcurrentTimeoutsIndependently()
	{
		// Arrange
		var saga = CreateSagaWithTimeoutStore();

		// Act - Schedule multiple timeouts with different delays
		var timeout1 = await saga.TestRequestTimeoutAsync<PaymentTimeout>(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(true);
		var timeout2 = await saga.TestRequestTimeoutAsync<ShippingTimeout>(TimeSpan.FromMinutes(10), CancellationToken.None).ConfigureAwait(true);
		var timeout3 = await saga.TestRequestTimeoutAsync<PaymentTimeout>(TimeSpan.FromMinutes(15), CancellationToken.None).ConfigureAwait(true);

		// Assert - All timeouts have unique IDs
		timeout1.ShouldNotBe(timeout2);
		timeout2.ShouldNotBe(timeout3);
		timeout1.ShouldNotBe(timeout3);

		// Assert - All timeouts are pending
		_timeoutStore.GetPendingCount().ShouldBe(3);

		// Act - Cancel one timeout
		await saga.TestCancelTimeoutAsync(timeout2, CancellationToken.None).ConfigureAwait(true);

		// Assert - Only cancelled timeout removed
		_timeoutStore.GetPendingCount().ShouldBe(2);
	}

	/// <summary>
	/// Tests that TimeoutStore property injection works correctly.
	/// </summary>
	[Fact]
	public async Task ThrowWhenTimeoutStoreNotConfigured()
	{
		// Arrange - Saga without timeout store
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);

		// TimeoutStore is NOT set (null)

		// Act & Assert - Should throw InvalidOperationException
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			saga.TestRequestTimeoutAsync<PaymentTimeout>(
				TimeSpan.FromMinutes(5),
				CancellationToken.None)).ConfigureAwait(true);

		exception.Message.ShouldContain("Timeout store not configured");
	}

	/// <summary>
	/// Creates a test saga with TimeoutStore property injected.
	/// </summary>
	private TestSaga CreateSagaWithTimeoutStore()
	{
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);

		// Property injection per Option B (AD-3)
		saga.SetTimeoutStore(_timeoutStore);

		return saga;
	}

	#region Test Infrastructure

	/// <summary>
	/// Test saga state for unit testing.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string OrderId { get; init; } = string.Empty;
	}

	/// <summary>
	/// Test saga that exposes protected timeout methods for testing.
	/// </summary>
	private sealed class TestSaga : SagaBase<TestSagaState>
	{
		public TestSaga(TestSagaState state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
		}

		/// <summary>
		/// Sets the timeout store for testing (simulates property injection).
		/// </summary>
		public void SetTimeoutStore(ISagaTimeoutStore store)
		{
			TimeoutStore = store;
		}

		/// <summary>
		/// Exposes protected RequestTimeoutAsync for testing.
		/// </summary>
		public Task<string> TestRequestTimeoutAsync<TTimeout>(TimeSpan delay, CancellationToken ct)
			where TTimeout : class, new()
		{
			return RequestTimeoutAsync<TTimeout>(delay, ct);
		}

		/// <summary>
		/// Exposes protected RequestTimeoutAsync with data for testing.
		/// </summary>
		public Task<string> TestRequestTimeoutAsync<TTimeout>(TimeSpan delay, TTimeout data, CancellationToken ct)
			where TTimeout : class
		{
			return RequestTimeoutAsync(delay, data, ct);
		}

		/// <summary>
		/// Exposes protected CancelTimeoutAsync for testing.
		/// </summary>
		public Task TestCancelTimeoutAsync(string timeoutId, CancellationToken ct)
		{
			return CancelTimeoutAsync(timeoutId, ct);
		}

		/// <summary>
		/// Exposes protected MarkCompletedAsync for testing.
		/// </summary>
		public Task TestMarkCompletedAsync(CancellationToken ct)
		{
			return MarkCompletedAsync(ct);
		}

		public override bool HandlesEvent(object eventMessage) => true;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Test timeout message type for payment scenarios.
	/// </summary>
	private sealed class PaymentTimeout
	{
		public string PaymentId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	/// <summary>
	/// Test timeout message type for shipping scenarios.
	/// </summary>
	private sealed class ShippingTimeout
	{
		public string ShipmentId { get; init; } = string.Empty;
	}

	#endregion Test Infrastructure
}
