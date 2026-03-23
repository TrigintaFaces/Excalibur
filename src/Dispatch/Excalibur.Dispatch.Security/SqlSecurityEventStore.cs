// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// SQL-based security event store implementation for persistent audit logging. Provides high-performance storage and querying of security
/// events using SQL Server or compatible databases.
/// </summary>
/// <remarks> Initializes a new instance of the SQL security event store. </remarks>
/// <param name="logger"> The logger instance. </param>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class SqlSecurityEventStore(
		ILogger<SqlSecurityEventStore> logger) : ISecurityEventStore, IDisposable
{
	private readonly ILogger<SqlSecurityEventStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	/// <summary>
	/// Stores security events in the SQL database.
	/// </summary>
	/// <param name="events"> The security events to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A task that represents the asynchronous store operation.</returns>
	/// <exception cref="ArgumentException">Thrown when invalid events are detected.</exception>
	/// <exception cref="InvalidOperationException">Thrown when storing security events fails.</exception>
	public async Task StoreEventsAsync(IEnumerable<SecurityEvent> events, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);

		var eventsList = events.ToList();
		if (eventsList.Count == 0)
		{
			return;
		}

		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Validate events
			foreach (var evt in eventsList)
			{
				if (evt.Id == Guid.Empty || string.IsNullOrWhiteSpace(evt.Description))
				{
					LogInvalidEvent(evt.Id, evt.Description);
				}
			}

			// Events validated but NOT persisted -- no backing store configured.
			// Register a real ISecurityEventStore implementation to persist audit events.
			LogEventsNotPersisted(eventsList.Count);
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <summary>
	/// Queries security events from the SQL database.
	/// </summary>
	/// <param name="query"> The query parameters. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The matching security events. </returns>
	/// <exception cref="ArgumentException">Thrown when MaxResults is less than or equal to zero, or when StartTime is greater than EndTime.</exception>
	/// <exception cref="InvalidOperationException">Thrown when querying security events fails.</exception>
	public async Task<IEnumerable<SecurityEvent>> QueryEventsAsync(SecurityEventQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (query.MaxResults <= 0)
			{
				throw new ArgumentException(
						Resources.SqlSecurityEventStore_MaxResultsMustBeGreaterThanZero,
						nameof(query));
			}

			if (query is { StartTime: not null, EndTime: not null } && query.StartTime > query.EndTime)
			{
				throw new ArgumentException(
						Resources.SqlSecurityEventStore_StartTimeAfterEndTime,
						nameof(query));
			}

			// No backing store configured -- no results available.
			// Register a real ISecurityEventStore to enable audit persistence.
			LogQueryNotSupported();

			return [];
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the SQL security event store and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"> true to release both managed and unmanaged resources; false to release only unmanaged resources. </param>
	[SuppressMessage("Performance", "MA0038:Make method static", Justification = "Dispose pattern requires instance method")]
	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			_semaphore?.Dispose();

			// Dispose managed resources
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.SqlStoreInvalidEvent, LogLevel.Warning, "Invalid security event detected: {SecurityEventId}, Description: {Description}")]
	private partial void LogInvalidEvent(Guid securityEventId, string? description);

	[LoggerMessage(SecurityEventId.SqlStoreEventsNotPersisted, LogLevel.Warning,
		"Security events validated but not persisted ({Count} events). Register a real ISecurityEventStore to enable audit persistence.")]
	private partial void LogEventsNotPersisted(int count);

	[LoggerMessage(SecurityEventId.SqlStoreQueryNotSupported, LogLevel.Warning,
		"Security event query executed against placeholder store -- no results available. Register a real ISecurityEventStore.")]
	private partial void LogQueryNotSupported();
}
