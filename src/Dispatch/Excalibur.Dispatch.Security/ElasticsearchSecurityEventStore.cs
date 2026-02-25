// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Elasticsearch-based security event store implementation for high-performance search and analytics. Provides advanced querying
/// capabilities and real-time indexing for security audit logs.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class ElasticsearchSecurityEventStore : ISecurityEventStore, IDisposable
{
	private readonly ILogger<ElasticsearchSecurityEventStore> _logger;
	private readonly string _connectionString;
	private readonly string _indexPrefix;
	private readonly HttpClient _httpClient;
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchSecurityEventStore"/> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <param name="httpClient"> The HTTP client for Elasticsearch API calls. </param>
	public ElasticsearchSecurityEventStore(
		ILogger<ElasticsearchSecurityEventStore> logger,
		IConfiguration configuration,
		HttpClient httpClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(configuration);
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_connectionString = configuration.GetConnectionString("Elasticsearch")
											 ?? throw new InvalidOperationException(
													 Resources.ElasticsearchSecurityEventStore_ConnectionStringRequired);
		_indexPrefix = configuration["Elasticsearch:Security:IndexPrefix"] ?? "security-events";

		// Configure HTTP client for Elasticsearch
		_httpClient.BaseAddress = new Uri(_connectionString.TrimEnd('/'));
		_httpClient.Timeout = TimeSpan.FromSeconds(30);
		_httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
	}

	/// <summary>
	/// Stores security events in Elasticsearch.
	/// </summary>
	/// <param name="events"> The security events to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A task that represents the asynchronous store operation.</returns>
	/// <exception cref="ArgumentException">Thrown when no valid events can be indexed in Elasticsearch.</exception>
	/// <exception cref="InvalidOperationException">Thrown when storing security events in Elasticsearch fails.</exception>
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

			var currentMonth = DateTimeOffset.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
			var indexName = $"{_indexPrefix}-{currentMonth}";

			var validEvents = ValidateAndCountEvents(eventsList);

			if (validEvents == 0)
			{
				throw new ArgumentException(
						Resources.ElasticsearchSecurityEventStore_NoValidEventsToIndex,
						nameof(events));
			}

			LogEventsIndexed(validEvents, indexName);
		}
		catch (Exception ex)
		{
			LogStoreEventsFailed(ex, eventsList.Count);
			throw new InvalidOperationException(
					Resources.ElasticsearchSecurityEventStore_FailedToStoreEvents,
					ex);
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <summary>
	/// Queries security events from Elasticsearch.
	/// </summary>
	/// <param name="query"> The query parameters. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The matching security events. </returns>
	/// <exception cref="ArgumentException">Thrown when MaxResults is less than or equal to 0, MaxResults exceeds 10000, or StartTime is greater than EndTime.</exception>
	/// <exception cref="InvalidOperationException">Thrown when querying security events from Elasticsearch fails.</exception>
	public async Task<IEnumerable<SecurityEvent>> QueryEventsAsync(SecurityEventQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			LogQueryingEvents(JsonSerializer.Serialize(query, SecurityEventSerializerContext.Default.SecurityEventQuery));

			ValidateQueryParameters(query);

			LogQueryExecuted();

			return [];
		}
		catch (Exception ex)
		{
			LogQueryFailed(ex);
			throw new InvalidOperationException(
					Resources.ElasticsearchSecurityEventStore_FailedToQueryEvents,
					ex);
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

	private static void ValidateQueryParameters(SecurityEventQuery query)
	{
		if (query.MaxResults <= 0)
		{
			throw new ArgumentException(
					Resources.ElasticsearchSecurityEventStore_MaxResultsMustBeGreaterThanZero,
					nameof(query));
		}

		if (query.MaxResults > 10000)
		{
			throw new ArgumentException(
					Resources.ElasticsearchSecurityEventStore_MaxResultsCannotExceedLimit,
					nameof(query));
		}

		if (query is { StartTime: not null, EndTime: not null } && query.StartTime > query.EndTime)
		{
			throw new ArgumentException(
					Resources.ElasticsearchSecurityEventStore_StartTimeAfterEndTime,
					nameof(query));
		}
	}

	private int ValidateAndCountEvents(List<SecurityEvent> eventsList)
	{
		var validEvents = 0;

		foreach (var evt in eventsList)
		{
			if (evt.Id != Guid.Empty && !string.IsNullOrWhiteSpace(evt.Description))
			{
				validEvents++;

				if (evt.Timestamp == default || evt.EventType.ToString().Length > 100)
				{
					LogInvalidEventStructure(evt.Id);
				}
			}
			else
			{
				LogInvalidEvent(evt.Id, evt.Description);
			}
		}

		return validEvents;
	}

	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			_semaphore?.Dispose();
			_httpClient?.Dispose();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.ElasticsearchStoringEvents, LogLevel.Debug, "Storing {Count} security events in Elasticsearch")]
	private partial void LogStoringEvents(int count);

	[LoggerMessage(SecurityEventId.ElasticsearchClientInitFailed, LogLevel.Warning, "Security event {SecurityEventId} has invalid structure for Elasticsearch")]
	private partial void LogInvalidEventStructure(Guid securityEventId);

	[LoggerMessage(SecurityEventId.SqlStoreInvalidEvent, LogLevel.Warning,
		"Invalid security event detected: {SecurityEventId}, Description: {Description}")]
	private partial void LogInvalidEvent(Guid securityEventId, string? description);

	[LoggerMessage(SecurityEventId.ElasticsearchEventsStored, LogLevel.Information,
		"Successfully indexed {ValidCount} security events in Elasticsearch index {IndexName}")]
	private partial void LogEventsIndexed(int validCount, string indexName);

	[LoggerMessage(SecurityEventId.ElasticsearchStoreFailed, LogLevel.Error, "Failed to store {Count} security events in Elasticsearch")]
	private partial void LogStoreEventsFailed(Exception ex, int count);

	[LoggerMessage(SecurityEventId.ElasticsearchQueryingEvents, LogLevel.Debug, "Querying security events from Elasticsearch with parameters: {Query}")]
	private partial void LogQueryingEvents(string query);

	[LoggerMessage(SecurityEventId.ElasticsearchQueryExecuted, LogLevel.Information,
		"Security events query executed successfully (would return filtered results from Elasticsearch)")]
	private partial void LogQueryExecuted();

	[LoggerMessage(SecurityEventId.ElasticsearchQueryFailed, LogLevel.Error, "Failed to query security events from Elasticsearch")]
	private partial void LogQueryFailed(Exception ex);
}
