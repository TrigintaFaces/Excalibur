// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Rectification;

/// <summary>
/// In-memory implementation of <see cref="IRectificationService"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores all rectification history in memory and is NOT suitable for production use.
/// Data is lost when the application restarts. For production scenarios, use a persistent
/// implementation backed by a database.
/// </para>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </para>
/// </remarks>
public sealed partial class RectificationService : IRectificationService
{
	private readonly ConcurrentDictionary<string, ConcurrentBag<RectificationRecord>> _history = new(StringComparer.Ordinal);
	private readonly IOptions<RectificationOptions> _options;
	private readonly ILogger<RectificationService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RectificationService"/> class.
	/// </summary>
	/// <param name="options">The rectification configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public RectificationService(
		IOptions<RectificationOptions> options,
		ILogger<RectificationService> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RectifyAsync(RectificationRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var record = new RectificationRecord(
			SubjectId: request.SubjectId,
			FieldName: request.FieldName,
			OldValue: request.OldValue,
			NewValue: request.NewValue,
			Reason: request.Reason,
			RectifiedAt: DateTimeOffset.UtcNow);

		var records = _history.GetOrAdd(request.SubjectId, static _ => []);
		records.Add(record);

		if (_options.Value.AuditAllChanges)
		{
			Log.DataRectified(_logger, request.SubjectId, request.FieldName);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<RectificationRecord>> GetRectificationHistoryAsync(
		string subjectId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subjectId);

		if (!_history.TryGetValue(subjectId, out var records))
		{
			return Task.FromResult<IReadOnlyList<RectificationRecord>>([]);
		}

		var sorted = records
			.OrderBy(r => r.RectifiedAt)
			.ToList();

		Log.HistoryRetrieved(_logger, subjectId, sorted.Count);

		return Task.FromResult<IReadOnlyList<RectificationRecord>>(sorted);
	}

	/// <summary>
	/// Gets the total number of subjects with rectification history.
	/// </summary>
	public int SubjectCount => _history.Count;

	/// <summary>
	/// Clears all rectification history from the store.
	/// </summary>
	public void Clear() => _history.Clear();

	private static partial class Log
	{
		[LoggerMessage(
			EventId = 2710,
			Level = LogLevel.Information,
			Message = "Data rectified for subject '{SubjectId}', field '{FieldName}'")]
		public static partial void DataRectified(
			ILogger logger,
			string subjectId,
			string fieldName);

		[LoggerMessage(
			EventId = 2711,
			Level = LogLevel.Debug,
			Message = "Retrieved {Count} rectification record(s) for subject '{SubjectId}'")]
		public static partial void HistoryRetrieved(
			ILogger logger,
			string subjectId,
			int count);
	}
}
