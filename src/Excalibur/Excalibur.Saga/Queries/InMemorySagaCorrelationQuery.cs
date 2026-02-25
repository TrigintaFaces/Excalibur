// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Queries;

/// <summary>
/// In-memory implementation of <see cref="ISagaCorrelationQuery"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores saga correlation data in memory using concurrent collections.
/// It is NOT suitable for production use where durability is required.
/// Data is lost when the application restarts.
/// </para>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </para>
/// </remarks>
public sealed partial class InMemorySagaCorrelationQuery : ISagaCorrelationQuery
{
	private readonly ConcurrentDictionary<string, SagaCorrelationEntry> _sagaEntries = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _correlationIndex = new(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentBag<string>>> _propertyIndex =
		new(StringComparer.Ordinal);

	private readonly IOptions<SagaCorrelationQueryOptions> _options;
	private readonly ILogger<InMemorySagaCorrelationQuery> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemorySagaCorrelationQuery"/> class.
	/// </summary>
	/// <param name="options">The query configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemorySagaCorrelationQuery(
		IOptions<SagaCorrelationQueryOptions> options,
		ILogger<InMemorySagaCorrelationQuery> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaQueryResult>> FindByCorrelationIdAsync(
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(correlationId);

		var options = _options.Value;

		if (!_correlationIndex.TryGetValue(correlationId, out var sagaIds))
		{
			return Task.FromResult<IReadOnlyList<SagaQueryResult>>([]);
		}

		var results = sagaIds
			.Distinct()
			.Select(id => _sagaEntries.TryGetValue(id, out var entry) ? entry : null)
			.Where(entry => entry is not null)
			.Where(entry => options.IncludeCompleted || entry.Status != SagaStatus.Completed)
			.Take(options.MaxResults)
			.Select(entry => new SagaQueryResult(
				entry.SagaId,
				entry.SagaName,
				entry.Status,
				entry.CorrelationId,
				entry.CreatedAt))
			.ToList();

		Log.FoundByCorrelationId(_logger, correlationId, results.Count);

		return Task.FromResult<IReadOnlyList<SagaQueryResult>>(results);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaQueryResult>> FindByPropertyAsync(
		string propertyName,
		object value,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(propertyName);
		ArgumentNullException.ThrowIfNull(value);

		var options = _options.Value;
		var valueKey = value.ToString() ?? string.Empty;

		if (!_propertyIndex.TryGetValue(propertyName, out var valueIndex) ||
		    !valueIndex.TryGetValue(valueKey, out var sagaIds))
		{
			return Task.FromResult<IReadOnlyList<SagaQueryResult>>([]);
		}

		var results = sagaIds
			.Distinct()
			.Select(id => _sagaEntries.TryGetValue(id, out var entry) ? entry : null)
			.Where(entry => entry is not null)
			.Where(entry => options.IncludeCompleted || entry.Status != SagaStatus.Completed)
			.Take(options.MaxResults)
			.Select(entry => new SagaQueryResult(
				entry.SagaId,
				entry.SagaName,
				entry.Status,
				entry.CorrelationId,
				entry.CreatedAt))
			.ToList();

		Log.FoundByProperty(_logger, propertyName, valueKey, results.Count);

		return Task.FromResult<IReadOnlyList<SagaQueryResult>>(results);
	}

	/// <summary>
	/// Indexes a saga instance for later correlation queries.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="sagaName">The saga type name.</param>
	/// <param name="correlationId">The correlation identifier.</param>
	/// <param name="status">The current saga status.</param>
	/// <param name="createdAt">The saga creation timestamp.</param>
	public void IndexSaga(
		string sagaId,
		string sagaName,
		string correlationId,
		SagaStatus status,
		DateTimeOffset createdAt)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);
		ArgumentException.ThrowIfNullOrEmpty(sagaName);
		ArgumentException.ThrowIfNullOrEmpty(correlationId);

		var entry = new SagaCorrelationEntry(sagaId, sagaName, correlationId, status, createdAt);
		_sagaEntries.AddOrUpdate(sagaId, entry, (_, _) => entry);

		var sagaIds = _correlationIndex.GetOrAdd(correlationId, static _ => []);
		sagaIds.Add(sagaId);
	}

	/// <summary>
	/// Indexes a named property value for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="propertyName">The property name to index.</param>
	/// <param name="value">The property value to index.</param>
	public void IndexProperty(string sagaId, string propertyName, object value)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);
		ArgumentException.ThrowIfNullOrEmpty(propertyName);
		ArgumentNullException.ThrowIfNull(value);

		var valueKey = value.ToString() ?? string.Empty;

		var valueIndex = _propertyIndex.GetOrAdd(propertyName, static _ => new(StringComparer.Ordinal));
		var sagaIds = valueIndex.GetOrAdd(valueKey, static _ => []);
		sagaIds.Add(sagaId);
	}

	/// <summary>
	/// Updates the status of a previously indexed saga.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="newStatus">The new saga status.</param>
	public void UpdateStatus(string sagaId, SagaStatus newStatus)
	{
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		if (_sagaEntries.TryGetValue(sagaId, out var existing))
		{
			var updated = existing with { Status = newStatus };
			_sagaEntries.TryUpdate(sagaId, updated, existing);
		}
	}

	/// <summary>
	/// Clears all indexed data from the store.
	/// </summary>
	public void Clear()
	{
		_sagaEntries.Clear();
		_correlationIndex.Clear();
		_propertyIndex.Clear();
	}

	/// <summary>
	/// Gets the total number of indexed saga instances.
	/// </summary>
	public int Count => _sagaEntries.Count;

	private sealed record SagaCorrelationEntry(
		string SagaId,
		string SagaName,
		string CorrelationId,
		SagaStatus Status,
		DateTimeOffset CreatedAt);

	private static partial class Log
	{
		[LoggerMessage(
			EventId = 3900,
			Level = LogLevel.Debug,
			Message = "Found {Count} saga(s) by correlation ID '{CorrelationId}'")]
		public static partial void FoundByCorrelationId(
			ILogger logger,
			string correlationId,
			int count);

		[LoggerMessage(
			EventId = 3901,
			Level = LogLevel.Debug,
			Message = "Found {Count} saga(s) by property '{PropertyName}' = '{Value}'")]
		public static partial void FoundByProperty(
			ILogger logger,
			string propertyName,
			string value,
			int count);
	}
}
