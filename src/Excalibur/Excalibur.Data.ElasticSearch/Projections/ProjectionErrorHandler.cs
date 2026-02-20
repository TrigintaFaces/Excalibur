// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Implements error handling for projection operations in ElasticSearch.
/// </summary>
public sealed class ProjectionErrorHandler : IProjectionErrorHandler
{
	private readonly ElasticsearchClient _client;
	private readonly ProjectionErrorHandlingOptions _settings;
	private readonly ILogger<ProjectionErrorHandler> _logger;
	private readonly Meter _meter;
	private readonly Counter<long> _errorCounter;
	private readonly Counter<long> _bulkErrorCounter;
	private readonly Histogram<double> _errorHandlingDuration;
	private readonly string _errorIndexName;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionErrorHandler" /> class.
	/// </summary>
	/// <param name="client"> The Elasticsearch client for storing error records. </param>
	/// <param name="options"> The configuration options containing error handling settings. </param>
	/// <param name="logger"> The logger for diagnostic information. </param>
	/// <param name="meterFactory"> The meter factory for creating metrics instruments. </param>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	public ProjectionErrorHandler(
		ElasticsearchClient client,
		IOptions<ElasticsearchConfigurationOptions> options,
		ILogger<ProjectionErrorHandler> logger,
		IMeterFactory meterFactory)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		ArgumentNullException.ThrowIfNull(options);
		_settings = options.Value.Projections?.ErrorHandling ?? new ProjectionErrorHandlingOptions();
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		ArgumentNullException.ThrowIfNull(meterFactory);
		_meter = meterFactory.Create("Excalibur.ElasticSearch.Projections");

		_errorCounter = _meter.CreateCounter<long>(
			"projection_errors_total",
			"count",
			"Total number of projection errors");

		_bulkErrorCounter = _meter.CreateCounter<long>(
			"projection_bulk_errors_total",
			"count",
			"Total number of bulk operation errors");

		_errorHandlingDuration = _meter.CreateHistogram<double>(
			"projection_error_handling_duration",
			"ms",
			"Duration of error handling operations");

		_errorIndexName = _settings.ErrorIndexName;
	}

	/// <inheritdoc />
	public async Task HandleProjectionErrorAsync(ProjectionErrorContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		using var activity = Activity.Current?.Source.StartActivity("HandleProjectionError");
		_ = (activity?.SetTag("projection.type", context.ProjectionType));
		_ = (activity?.SetTag("operation.type", context.OperationType));
		_ = (activity?.SetTag("document.id", context.DocumentId));

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Log the error with full context
			_logger.LogError(
				context.Exception,
				"Projection error: {ProjectionType} {OperationType} failed for document {DocumentId} in index {IndexName} after {AttemptCount} attempts",
				context.ProjectionType,
				context.OperationType,
				context.DocumentId ?? "unknown",
				context.IndexName,
				context.AttemptCount);

			// Record metrics
			_errorCounter.Add(
				1,
				new KeyValuePair<string, object?>("projection_type", context.ProjectionType),
				new KeyValuePair<string, object?>("operation_type", context.OperationType),
				new KeyValuePair<string, object?>("index_name", context.IndexName));

			// Store error record for analysis
			if (_settings.StoreErrors)
			{
				var errorRecord = new ProjectionErrorRecord
				{
					Id = Guid.NewGuid().ToString(),
					Timestamp = DateTimeOffset.UtcNow,
					ProjectionType = context.ProjectionType,
					OperationType = context.OperationType,
					DocumentId = context.DocumentId,
					IndexName = context.IndexName,
					ErrorMessage = context.Exception.Message,
					ExceptionDetails = context.Exception.ToString(),
					AttemptCount = context.AttemptCount,
					IsResolved = false,
					Metadata = context.Metadata,
				};

				await StoreErrorRecordAsync(errorRecord, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to handle projection error for {ProjectionType}", context.ProjectionType);
		}
		finally
		{
			_errorHandlingDuration.Record(
				stopwatch.ElapsedMilliseconds,
				new KeyValuePair<string, object?>("projection_type", context.ProjectionType));
		}
	}

	/// <inheritdoc />
	public async Task HandleBulkOperationErrorsAsync(BulkOperationErrorContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		using var activity = Activity.Current?.Source.StartActivity("HandleBulkOperationErrors");
		_ = (activity?.SetTag("projection.type", context.ProjectionType));
		_ = (activity?.SetTag("operation.type", context.OperationType));
		_ = (activity?.SetTag("total_documents", context.TotalDocuments));
		_ = (activity?.SetTag("failed_documents", context.Failures.Count));

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Log summary of bulk operation errors
			_logger.LogWarning(
				"Bulk operation partial failure: {ProjectionType} {OperationType} - {FailedCount}/{TotalCount} documents failed in index {IndexName}",
				context.ProjectionType,
				context.OperationType,
				context.Failures.Count,
				context.TotalDocuments,
				context.IndexName);

			// Record metrics
			_bulkErrorCounter.Add(
				context.Failures.Count,
				new KeyValuePair<string, object?>("projection_type", context.ProjectionType),
				new KeyValuePair<string, object?>("operation_type", context.OperationType),
				new KeyValuePair<string, object?>("index_name", context.IndexName));

			// Log individual failures if detailed logging is enabled
			if (_settings.LogDetailedErrors)
			{
				foreach (var failure in context.Failures.Take(10)) // Limit to prevent log flooding
				{
					_logger.LogError(
						"Bulk operation failure for document {DocumentId}: {ErrorMessage} ({ErrorType})",
						failure.DocumentId,
						failure.ErrorMessage,
						failure.ErrorType ?? "Unknown");
				}

				if (context.Failures.Count > 10)
				{
					_logger.LogWarning(
						"Additional {Count} bulk operation failures not logged. Enable trace logging for full details.",
						context.Failures.Count - 10);
				}
			}

			// Store error records for failed documents
			if (_settings.StoreErrors && context.Failures.Any())
			{
				var errorRecords = context.Failures.Select(failure => new ProjectionErrorRecord
				{
					Id = Guid.NewGuid().ToString(),
					Timestamp = DateTimeOffset.UtcNow,
					ProjectionType = context.ProjectionType,
					OperationType = context.OperationType,
					DocumentId = failure.DocumentId,
					IndexName = context.IndexName,
					ErrorMessage = failure.ErrorMessage,
					ExceptionDetails = failure.ErrorType,
					AttemptCount = 1,
					IsResolved = false,
					Metadata = context.Metadata,
				}).ToList();

				await StoreBulkErrorRecordsAsync(errorRecords, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to handle bulk operation errors for {ProjectionType}", context.ProjectionType);
		}
		finally
		{
			_errorHandlingDuration.Record(
				stopwatch.ElapsedMilliseconds,
				new KeyValuePair<string, object?>("projection_type", context.ProjectionType),
				new KeyValuePair<string, object?>("operation_type", "bulk"));
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<ProjectionErrorRecord>> GetProjectionErrorsAsync(
		DateTime fromDate,
		DateTime toDate,
		string? projectionType,
		int maxResults,
		CancellationToken cancellationToken)
	{
		try
		{
			var searchRequest = new SearchRequest(_errorIndexName)
			{
				Size = maxResults,
				Query = BuildErrorQuery(fromDate, toDate, projectionType),
				Sort = new SortOptions[] { SortOptions.Field(new Field("timestamp"), new FieldSort { Order = SortOrder.Desc }) },
			};

			var response = await _client.SearchAsync<ProjectionErrorRecord>(searchRequest, cancellationToken).ConfigureAwait(false);

			if (!response.IsValidResponse || response.Documents == null)
			{
				var errorMessage = response.ApiCallDetails?.OriginalException?.Message ?? "Unknown error";
				_logger.LogWarning("Failed to retrieve projection error records: {Error}", errorMessage);
				return [];
			}

			return response.Documents;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving projection error records");
			return [];
		}
	}

	/// <inheritdoc />
	public async Task<int> MarkErrorsAsResolvedAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(errorIds);

		var errorIdList = errorIds.ToList();
		if (errorIdList.Count == 0)
		{
			return 0;
		}

		try
		{
			var updateByQueryRequest = new UpdateByQueryRequest(_errorIndexName)
			{
				Query =
					new BoolQuery
					{
						Should = [.. errorIdList.Select(id => new TermQuery(new Field("id")) { Value = id }).Cast<Query>()],
					},
				Script = new Script
				{
					Source = "ctx._source.isResolved = true; ctx._source.resolvedAt = params.now",
					Params = new Dictionary<string, object>(StringComparer.Ordinal) { ["now"] = DateTimeOffset.UtcNow },
				},
			};

			var response = await _client.UpdateByQueryAsync(updateByQueryRequest, cancellationToken).ConfigureAwait(false);

			if (!response.IsValidResponse)
			{
				var errorMessage = response.ApiCallDetails?.OriginalException?.Message ?? "Unknown error";
				_logger.LogWarning("Failed to mark errors as resolved: {Error}", errorMessage);
				return 0;
			}

			var updatedCount = (int)(response.Updated ?? 0);
			_logger.LogInformation("Marked {Count} projection errors as resolved", updatedCount);

			return updatedCount;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error marking errors as resolved");
			return 0;
		}
	}

	/// <summary>
	/// Builds a query for retrieving error records within a date range and optional projection type filter.
	/// </summary>
	private static Query BuildErrorQuery(DateTime fromDate, DateTime toDate, string? projectionType)
	{
		var queries = new List<Query> { new DateRangeQuery(new Field("timestamp")) { Gte = fromDate, Lte = toDate } };

		if (!string.IsNullOrWhiteSpace(projectionType))
		{
			queries.Add(new TermQuery(new Field("projectionType")) { Value = projectionType });
		}

		return queries.Count == 1
			? queries[0]
			: new BoolQuery { Must = queries.ToArray() };
	}

	/// <summary>
	/// Stores a single error record in Elasticsearch.
	/// </summary>
	private async Task StoreErrorRecordAsync(ProjectionErrorRecord record, CancellationToken cancellationToken)
	{
		try
		{
			await EnsureErrorIndexExistsAsync(cancellationToken).ConfigureAwait(false);

			var indexRequest = new IndexRequest<ProjectionErrorRecord>(_errorIndexName) { Document = record };

			var response = await _client.IndexAsync(indexRequest, cancellationToken).ConfigureAwait(false);

			if (!response.IsValidResponse)
			{
				_logger.LogWarning(
					"Failed to store projection error record: {Error}",
					response.ApiCallDetails?.OriginalException?.Message ?? "Unknown error");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error storing projection error record");
		}
	}

	/// <summary>
	/// Stores multiple error records in bulk.
	/// </summary>
	private async Task StoreBulkErrorRecordsAsync(IList<ProjectionErrorRecord> records, CancellationToken cancellationToken)
	{
		try
		{
			await EnsureErrorIndexExistsAsync(cancellationToken).ConfigureAwait(false);

			var bulkRequest = new BulkRequest(_errorIndexName)
			{
				Operations = [.. records.Select(static record =>
						new BulkIndexOperation<ProjectionErrorRecord>(record) { Id = record.Id }).Cast<IBulkOperation>().ToList(),],
			};

			var response = await _client.BulkAsync(bulkRequest, cancellationToken).ConfigureAwait(false);

			if (!response.IsValidResponse)
			{
				_logger.LogWarning(
					"Failed to store bulk projection error records: {Error}",
					response.ApiCallDetails?.OriginalException?.Message ?? "Unknown error");
			}
			else if (response.Errors)
			{
				var failedCount = response.Items?.Count(static i => i.Error != null) ?? 0;
				_logger.LogWarning(
					"Partial failure storing projection error records: {FailedCount}/{TotalCount} failed",
					failedCount, records.Count);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error storing bulk projection error records");
		}
	}

	/// <summary>
	/// Ensures the error index exists with appropriate mapping.
	/// </summary>
	private async Task EnsureErrorIndexExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client.Indices.ExistsAsync(_errorIndexName, cancellationToken).ConfigureAwait(false);
		if (existsResponse.Exists)
		{
			return;
		}

		var createRequest = new CreateIndexRequest(_errorIndexName)
		{
			Mappings = new TypeMapping
			{
				Properties = new Properties
				{
					["id"] = new KeywordProperty(),
					["timestamp"] = new DateProperty(),
					["projectionType"] = new KeywordProperty(),
					["operationType"] = new KeywordProperty(),
					["documentId"] = new KeywordProperty(),
					["indexName"] = new KeywordProperty(),
					["errorMessage"] =
						new TextProperty { Fields = new Properties { ["keyword"] = new KeywordProperty() } },
					["exceptionDetails"] = new TextProperty(),
					["attemptCount"] = new IntegerNumberProperty(),
					["isResolved"] = new BooleanProperty(),
					["resolvedAt"] = new DateProperty(),
					["metadata"] = new ObjectProperty(),
				},
			},
			Settings = new IndexSettings
			{
				NumberOfShards = 1,
				NumberOfReplicas = 0,

				// Lifecycle management would be configured separately in the new client
			},
		};

		var createResponse = await _client.Indices.CreateAsync(createRequest, cancellationToken).ConfigureAwait(false);

		if (!createResponse.IsValidResponse)
		{
			var errorMessage = createResponse.ApiCallDetails?.OriginalException?.Message ?? "Unknown error";
			_logger.LogWarning(
				"Failed to create projection error index {IndexName}: {Error}",
				_errorIndexName, errorMessage);
		}
		else
		{
			_logger.LogInformation("Created projection error index {IndexName}", _errorIndexName);
		}
	}
}
