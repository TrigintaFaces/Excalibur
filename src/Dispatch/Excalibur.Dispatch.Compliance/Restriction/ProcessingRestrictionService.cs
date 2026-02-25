// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Restriction;

/// <summary>
/// In-memory implementation of <see cref="IProcessingRestrictionService"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores all restriction data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts. For production scenarios, use a persistent
/// implementation backed by a database.
/// </para>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </para>
/// </remarks>
public sealed partial class ProcessingRestrictionService : IProcessingRestrictionService
{
	private readonly ConcurrentDictionary<string, RestrictionEntry> _restrictions = new(StringComparer.Ordinal);
	private readonly IOptions<ProcessingRestrictionOptions> _options;
	private readonly ILogger<ProcessingRestrictionService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProcessingRestrictionService"/> class.
	/// </summary>
	/// <param name="options">The processing restriction configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public ProcessingRestrictionService(
		IOptions<ProcessingRestrictionOptions> options,
		ILogger<ProcessingRestrictionService> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RestrictAsync(string subjectId, RestrictionReason reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subjectId);

		var now = DateTimeOffset.UtcNow;
		var duration = _options.Value.DefaultRestrictionDuration;

		var entry = new RestrictionEntry(
			SubjectId: subjectId,
			Reason: reason,
			RestrictedAt: now,
			ExpiresAt: now.Add(duration));

		_restrictions.AddOrUpdate(subjectId, entry, (_, _) => entry);

		Log.ProcessingRestricted(_logger, subjectId, reason);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task UnrestrictAsync(string subjectId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subjectId);

		if (_restrictions.TryRemove(subjectId, out _))
		{
			Log.ProcessingUnrestricted(_logger, subjectId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> IsRestrictedAsync(string subjectId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subjectId);

		if (!_restrictions.TryGetValue(subjectId, out var entry))
		{
			return Task.FromResult(false);
		}

		// Check if the restriction has expired
		if (entry.ExpiresAt < DateTimeOffset.UtcNow)
		{
			_restrictions.TryRemove(subjectId, out _);
			return Task.FromResult(false);
		}

		return Task.FromResult(true);
	}

	/// <summary>
	/// Gets the total number of active restrictions.
	/// </summary>
	public int Count => _restrictions.Count;

	/// <summary>
	/// Clears all restrictions from the store.
	/// </summary>
	public void Clear() => _restrictions.Clear();

	private sealed record RestrictionEntry(
		string SubjectId,
		RestrictionReason Reason,
		DateTimeOffset RestrictedAt,
		DateTimeOffset ExpiresAt);

	private static partial class Log
	{
		[LoggerMessage(
			EventId = 2700,
			Level = LogLevel.Information,
			Message = "Processing restricted for subject '{SubjectId}' with reason '{Reason}'")]
		public static partial void ProcessingRestricted(
			ILogger logger,
			string subjectId,
			RestrictionReason reason);

		[LoggerMessage(
			EventId = 2701,
			Level = LogLevel.Information,
			Message = "Processing restriction removed for subject '{SubjectId}'")]
		public static partial void ProcessingUnrestricted(
			ILogger logger,
			string subjectId);
	}
}
