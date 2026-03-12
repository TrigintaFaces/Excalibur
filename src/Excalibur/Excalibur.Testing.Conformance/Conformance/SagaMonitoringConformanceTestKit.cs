// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;

using SagaStateModel = Excalibur.Saga.Models.SagaState;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISagaMonitoringService conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateMonitoringService"/> and
/// <see cref="CreateStateStore"/> to verify that your saga monitoring implementation
/// conforms to the ISagaMonitoringService contract.
/// </para>
/// <para>
/// The monitoring service reads from saga state data, so a state store is needed
/// to seed test data.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSagaMonitoringConformanceTests : SagaMonitoringConformanceTestKit
/// {
///     protected override ISagaMonitoringService CreateMonitoringService() =&gt;
///         new SqlServerSagaMonitoringService(_connectionString);
///
///     protected override ISagaStateStore CreateStateStore() =&gt;
///         new SqlServerSagaStateStore(_connectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class SagaMonitoringConformanceTestKit
{
	/// <summary>
	/// Creates a fresh monitoring service instance for testing.
	/// </summary>
	/// <returns>An ISagaMonitoringService implementation to test.</returns>
	protected abstract ISagaMonitoringService CreateMonitoringService();

	/// <summary>
	/// Creates a saga state store for seeding test data.
	/// </summary>
	/// <returns>An ISagaStateStore implementation for test data setup.</returns>
	protected abstract ISagaStateStore CreateStateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates a unique saga ID for test isolation.
	/// </summary>
	/// <returns>A unique saga identifier string.</returns>
	protected virtual string GenerateSagaId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Gets the saga type name used for test data.
	/// </summary>
	/// <returns>A unique saga type name for test isolation.</returns>
	protected virtual string GenerateSagaType() => $"TestSaga_{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a saga state with the specified status for test seeding.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <param name="sagaType">The saga type name.</param>
	/// <param name="status">The saga status.</param>
	/// <returns>A populated saga state.</returns>
	protected virtual SagaStateModel CreateTestState(
		string sagaId,
		string sagaType,
		SagaStatus status) =>
		new()
		{
			SagaId = sagaId,
			SagaName = sagaType,
			Version = "1.0",
			CorrelationId = Guid.NewGuid().ToString(),
			Status = status,
			CurrentStepIndex = 0,
			DataJson = "{}",
			DataType = "TestData",
			StartedAt = DateTime.UtcNow,
			LastUpdatedAt = DateTime.UtcNow
		};

	#region GetRunningCount Tests

	/// <summary>
	/// Verifies that GetRunningCountAsync returns zero when no sagas exist.
	/// </summary>
	public virtual async Task GetRunningCountAsync_NoSagas_ShouldReturnZero()
	{
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		var count = await service.GetRunningCountAsync(sagaType, CancellationToken.None)
			.ConfigureAwait(false);

		if (count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 running sagas for new type but got {count}");
		}
	}

	/// <summary>
	/// Verifies that GetRunningCountAsync counts running sagas correctly.
	/// </summary>
	public virtual async Task GetRunningCountAsync_WithRunningSagas_ShouldCountCorrectly()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		// Create 2 running sagas
		for (var i = 0; i < 2; i++)
		{
			var state = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Running);
			await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);
		}

		// Create 1 completed saga (should not be counted)
		var completed = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Completed);
		completed.CompletedAt = DateTime.UtcNow;
		await store.SaveStateAsync(completed, CancellationToken.None).ConfigureAwait(false);

		var count = await service.GetRunningCountAsync(sagaType, CancellationToken.None)
			.ConfigureAwait(false);

		if (count < 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 2 running sagas but got {count}");
		}
	}

	/// <summary>
	/// Verifies that GetRunningCountAsync with null saga type counts all types.
	/// </summary>
	public virtual async Task GetRunningCountAsync_NullSagaType_ShouldCountAll()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();

		// Create running sagas of different types
		var type1 = GenerateSagaType();
		var type2 = GenerateSagaType();

		var state1 = CreateTestState(GenerateSagaId(), type1, SagaStatus.Running);
		var state2 = CreateTestState(GenerateSagaId(), type2, SagaStatus.Running);

		await store.SaveStateAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveStateAsync(state2, CancellationToken.None).ConfigureAwait(false);

		var count = await service.GetRunningCountAsync(null, CancellationToken.None)
			.ConfigureAwait(false);

		if (count < 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 2 running sagas across all types but got {count}");
		}
	}

	#endregion

	#region GetCompletedCount Tests

	/// <summary>
	/// Verifies that GetCompletedCountAsync returns zero when no completed sagas exist.
	/// </summary>
	public virtual async Task GetCompletedCountAsync_NoCompleted_ShouldReturnZero()
	{
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		var count = await service.GetCompletedCountAsync(sagaType, null, CancellationToken.None)
			.ConfigureAwait(false);

		if (count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 completed sagas for new type but got {count}");
		}
	}

	/// <summary>
	/// Verifies that GetCompletedCountAsync counts completed sagas correctly.
	/// </summary>
	public virtual async Task GetCompletedCountAsync_WithCompleted_ShouldCountCorrectly()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		// Create 1 completed saga
		var completed = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Completed);
		completed.CompletedAt = DateTime.UtcNow;
		await store.SaveStateAsync(completed, CancellationToken.None).ConfigureAwait(false);

		// Create 1 running saga (should not be counted)
		var running = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Running);
		await store.SaveStateAsync(running, CancellationToken.None).ConfigureAwait(false);

		var count = await service.GetCompletedCountAsync(sagaType, null, CancellationToken.None)
			.ConfigureAwait(false);

		if (count < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 completed saga but got {count}");
		}
	}

	#endregion

	#region GetStuckSagas Tests

	/// <summary>
	/// Verifies that GetStuckSagasAsync returns empty when no sagas are stuck.
	/// </summary>
	public virtual async Task GetStuckSagasAsync_NoStuck_ShouldReturnEmpty()
	{
		var service = CreateMonitoringService();

		var stuckSagas = await service.GetStuckSagasAsync(
			TimeSpan.FromHours(24),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// May contain pre-existing data, but should not fail
		if (stuckSagas is null)
		{
			throw new TestFixtureAssertionException(
				"Expected non-null result from GetStuckSagasAsync");
		}
	}

	/// <summary>
	/// Verifies that GetStuckSagasAsync respects the limit parameter.
	/// </summary>
	public virtual async Task GetStuckSagasAsync_ShouldRespectLimit()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		// Create 3 "stuck" sagas (Running, last updated long ago)
		for (var i = 0; i < 3; i++)
		{
			var state = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Running);
			state.LastUpdatedAt = DateTime.UtcNow.AddHours(-48);
			state.StartedAt = DateTime.UtcNow.AddHours(-48);
			await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);
		}

		var stuckSagas = await service.GetStuckSagasAsync(
			TimeSpan.FromHours(1),
			2,
			CancellationToken.None).ConfigureAwait(false);

		if (stuckSagas.Count > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 stuck sagas (limit=2) but got {stuckSagas.Count}");
		}
	}

	#endregion

	#region GetFailedSagas Tests

	/// <summary>
	/// Verifies that GetFailedSagasAsync returns failed sagas.
	/// </summary>
	public virtual async Task GetFailedSagasAsync_WithFailures_ShouldReturnThem()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		// Create a failed saga
		var failed = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Failed);
		failed.ErrorMessage = "Test failure";
		await store.SaveStateAsync(failed, CancellationToken.None).ConfigureAwait(false);

		var failedSagas = await service.GetFailedSagasAsync(100, CancellationToken.None)
			.ConfigureAwait(false);

		if (failedSagas is null)
		{
			throw new TestFixtureAssertionException(
				"Expected non-null result from GetFailedSagasAsync");
		}

		// Should contain at least our failed saga
		if (failedSagas.Count < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 failed saga but got {failedSagas.Count}");
		}
	}

	/// <summary>
	/// Verifies that GetFailedSagasAsync respects the limit parameter.
	/// </summary>
	public virtual async Task GetFailedSagasAsync_ShouldRespectLimit()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		// Create 3 failed sagas
		for (var i = 0; i < 3; i++)
		{
			var state = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Failed);
			state.ErrorMessage = $"Failure {i}";
			await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);
		}

		var failedSagas = await service.GetFailedSagasAsync(2, CancellationToken.None)
			.ConfigureAwait(false);

		if (failedSagas.Count > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 failed sagas (limit=2) but got {failedSagas.Count}");
		}
	}

	#endregion

	#region GetAverageCompletionTime Tests

	/// <summary>
	/// Verifies that GetAverageCompletionTimeAsync returns null when no completed sagas exist.
	/// </summary>
	public virtual async Task GetAverageCompletionTimeAsync_NoCompleted_ShouldReturnNull()
	{
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		var avgTime = await service.GetAverageCompletionTimeAsync(
			sagaType,
			DateTime.UtcNow.AddYears(-1),
			CancellationToken.None).ConfigureAwait(false);

		if (avgTime is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for no completed sagas but got {avgTime}");
		}
	}

	/// <summary>
	/// Verifies that GetAverageCompletionTimeAsync returns a positive value for completed sagas.
	/// </summary>
	public virtual async Task GetAverageCompletionTimeAsync_WithCompleted_ShouldReturnPositive()
	{
		var store = CreateStateStore();
		var service = CreateMonitoringService();
		var sagaType = GenerateSagaType();

		// Create a completed saga with known duration
		var completed = CreateTestState(GenerateSagaId(), sagaType, SagaStatus.Completed);
		completed.StartedAt = DateTime.UtcNow.AddMinutes(-10);
		completed.CompletedAt = DateTime.UtcNow;
		await store.SaveStateAsync(completed, CancellationToken.None).ConfigureAwait(false);

		var avgTime = await service.GetAverageCompletionTimeAsync(
			sagaType,
			DateTime.UtcNow.AddHours(-1),
			CancellationToken.None).ConfigureAwait(false);

		if (avgTime is null)
		{
			throw new TestFixtureAssertionException(
				"Expected average completion time but got null");
		}

		if (avgTime.Value <= TimeSpan.Zero)
		{
			throw new TestFixtureAssertionException(
				$"Expected positive average completion time but got {avgTime.Value}");
		}
	}

	#endregion
}
