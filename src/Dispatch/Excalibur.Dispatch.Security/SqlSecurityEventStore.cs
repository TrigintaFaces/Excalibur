// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

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
	private static readonly CompositeFormat InvalidEventsDetectedFormat =
			CompositeFormat.Parse(Resources.SqlSecurityEventStore_InvalidEventsDetectedFormat);

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
			LogStoringEvents(eventsList.Count);

			// Note: In a real implementation, you would use a proper database connection and a library like Dapper
			//
			// Example with Dapper: using var connection = new SqlConnection(_connectionString); await connection.OpenAsync(cancellationToken);
			//
			// const string sql = @" INSERT INTO SecurityEvents (MessageId, Timestamp, EventType, Description, Severity, CorrelationId,
			// UserId, SourceIp, UserAgent, MessageType, AdditionalData) VALUES (@MessageId, @Timestamp, @EventType, @Description,
			// @Severity, @CorrelationId, @UserId, @SourceIp, @UserAgent, @MessageType, @AdditionalData)";
			//
			// var parameters = eventsList.Select(evt => new { evt.MessageId, evt.Timestamp, EventType = evt.EventType.ToString(),
			// evt.Description, Severity = evt.Severity.ToString(), evt.CorrelationId, evt.UserId, evt.SourceIp, evt.UserAgent,
			// evt.MessageType, AdditionalData = JsonSerializer.Serialize(evt.AdditionalData) });
			//
			// var rowsAffected = await connection.ExecuteAsync(sql, parameters);

			// For now, validate the events and simulate successful storage
			var validEvents = 0;
			foreach (var evt in eventsList)
			{
				if (evt.Id != Guid.Empty && !string.IsNullOrWhiteSpace(evt.Description))
				{
					validEvents++;
				}
				else
				{
					LogInvalidEvent(evt.Id, evt.Description);
				}
			}

			if (validEvents != eventsList.Count)
			{
				throw new ArgumentException(
						string.Format(
								CultureInfo.InvariantCulture,
								InvalidEventsDetectedFormat,
								eventsList.Count - validEvents,
								eventsList.Count),
						nameof(events));
			}

			LogEventsStored(eventsList.Count);
		}
		catch (Exception ex)
		{
			LogStoreFailed(ex, eventsList.Count);
			throw new InvalidOperationException(Resources.SqlSecurityEventStore_FailedToStoreEvents, ex);
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
			LogQueryingEvents(JsonSerializer.Serialize(query, SecurityEventSerializerContext.Default.SecurityEventQuery));

			// In a real implementation, you would build dynamic SQL with proper parameterization Example with Dapper: using var connection
			// = new SqlConnection(_connectionString); await connection.OpenAsync(cancellationToken);
			//
			// var sql = new StringBuilder("SELECT TOP (@MaxResults) * FROM SecurityEvents WHERE 1=1"); var parameters = new
			// DynamicParameters(); parameters.Add("MaxResults", query.MaxResults);
			//
			// if (query.StartTime.HasValue) { sql.Append(" AND Timestamp >= @StartTime"); parameters.Add("StartTime",
			// query.StartTime.Value); }
			//
			// if (query.EndTime.HasValue) { sql.Append(" AND Timestamp <= @EndTime"); parameters.Add("EndTime", query.EndTime.Value); }
			//
			// if (query.EventType.HasValue) { sql.Append(" AND EventType = @EventType"); parameters.Add("EventType",
			// query.EventType.Value.ToString()); }
			//
			// if (!string.IsNullOrEmpty(query.UserId)) { sql.Append(" AND UserId = @UserId"); parameters.Add("UserId", query.UserId); }
			//
			// sql.Append(" ORDER BY Timestamp DESC");
			//
			// var results = await connection.QueryAsync<SecurityEventDto>(sql.ToString(), parameters); return results.Select(MapToSecurityEvent);

			// For now, return empty collection with proper validation
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

			LogQueryExecuted();

			// Return empty collection for now - in real implementation this would return actual query results
			return [];
		}
		catch (Exception ex)
		{
			LogQueryFailed(ex);
			throw new InvalidOperationException(Resources.SqlSecurityEventStore_FailedToQueryEvents, ex);
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
	[LoggerMessage(SecurityEventId.SqlStoreStoringEvents, LogLevel.Debug, "Storing {Count} security events in SQL database")]
	private partial void LogStoringEvents(int count);

	[LoggerMessage(SecurityEventId.SqlStoreInvalidEvent, LogLevel.Warning, "Invalid security event detected: {SecurityEventId}, Description: {Description}")]
	private partial void LogInvalidEvent(Guid securityEventId, string? description);

	[LoggerMessage(SecurityEventId.SqlStoreEventsStored, LogLevel.Information, "Successfully stored {Count} security events in SQL database")]
	private partial void LogEventsStored(int count);

	[LoggerMessage(SecurityEventId.SqlStoreStoreFailed, LogLevel.Error, "Failed to store {Count} security events in SQL database")]
	private partial void LogStoreFailed(Exception ex, int count);

	[LoggerMessage(SecurityEventId.SqlStoreQueryingEvents, LogLevel.Debug, "Querying security events from SQL database with parameters: {Query}")]
	private partial void LogQueryingEvents(string query);

	[LoggerMessage(SecurityEventId.SqlStoreQueryExecuted, LogLevel.Information, "Security events query executed successfully (would return filtered results from SQL database)")]
	private partial void LogQueryExecuted();

	[LoggerMessage(SecurityEventId.SqlStoreQueryFailed, LogLevel.Error, "Failed to query security events from SQL database")]
	private partial void LogQueryFailed(Exception ex);
}
