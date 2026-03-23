// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Storage;

/// <summary>
/// In-memory implementation of <see cref="ISagaMonitoringService"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores saga instance information in a thread-safe
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Data is not persisted
/// and is lost when the process exits.
/// </para>
/// <para>
/// Use <see cref="RecordSaga"/> to populate monitoring data in test scenarios.
/// For production use, prefer <c>SqlServerSagaMonitoringService</c>.
/// </para>
/// </remarks>
public sealed class InMemorySagaMonitoringService : ISagaMonitoringService
{
	private readonly ConcurrentDictionary<Guid, SagaInstanceInfo> _sagas = new();

	/// <summary>
	/// Records a saga instance for monitoring. Use this in tests to populate data.
	/// </summary>
	/// <param name="info">The saga instance information to record.</param>
	public void RecordSaga(SagaInstanceInfo info)
	{
		ArgumentNullException.ThrowIfNull(info);
		_ = _sagas.AddOrUpdate(info.SagaId, info, static (_, newValue) => newValue);
	}

	/// <inheritdoc />
	public Task<int> GetRunningCountAsync(string? sagaType, CancellationToken cancellationToken)
	{
		var count = _sagas.Values
			.Count(s => !s.IsCompleted
				&& (sagaType is null || string.Equals(s.SagaType, sagaType, StringComparison.Ordinal)));
		return Task.FromResult(count);
	}

	/// <inheritdoc />
	public Task<int> GetCompletedCountAsync(string? sagaType, DateTimeOffset? since, CancellationToken cancellationToken)
	{
		var count = _sagas.Values
			.Count(s => s.IsCompleted
				&& (sagaType is null || string.Equals(s.SagaType, sagaType, StringComparison.Ordinal))
				&& (since is null || s.CompletedAt >= since));
		return Task.FromResult(count);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaInstanceInfo>> GetStuckSagasAsync(
		TimeSpan threshold,
		int limit,
		CancellationToken cancellationToken)
	{
		var cutoff = DateTimeOffset.UtcNow - threshold;
		IReadOnlyList<SagaInstanceInfo> result = _sagas.Values
			.Where(s => !s.IsCompleted && s.LastUpdatedAt < cutoff)
			.OrderBy(static s => s.LastUpdatedAt)
			.Take(limit)
			.ToList();
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaInstanceInfo>> GetFailedSagasAsync(
		int limit,
		CancellationToken cancellationToken)
	{
		IReadOnlyList<SagaInstanceInfo> result = _sagas.Values
			.Where(static s => s.FailureReason is not null)
			.OrderByDescending(static s => s.LastUpdatedAt)
			.Take(limit)
			.ToList();
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<TimeSpan?> GetAverageCompletionTimeAsync(
		string sagaType,
		DateTimeOffset since,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);

		var completedSagas = _sagas.Values
			.Where(s => s.IsCompleted
				&& s.CompletedAt.HasValue
				&& string.Equals(s.SagaType, sagaType, StringComparison.Ordinal)
				&& s.CompletedAt >= since)
			.ToList();

		if (completedSagas.Count == 0)
		{
			return Task.FromResult<TimeSpan?>(null);
		}

		var averageTicks = completedSagas
			.Average(s => (s.CompletedAt!.Value - s.CreatedAt).Ticks);
		return Task.FromResult<TimeSpan?>(TimeSpan.FromTicks((long)averageTicks));
	}
}
