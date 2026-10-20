// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Data.ElasticSearch.Resilience;

using Elastic.Transport;

using Polly;

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
	private readonly ResiliencePipeline<bool> _indexRetry;
	private readonly ResiliencePipeline<bool> _bulkRetry;

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
			"{errors}",
			"Total number of projection errors");

		_bulkErrorCounter = _meter.CreateCounter<long>(
			"projection_bulk_errors_total",
			"{errors}",
			"Total number of bulk operation errors");

		_errorHandlingDuration = _meter.CreateHistogram<double>(
			"projection_error_handling_duration",
			"ms",
			"Duration of error handling operations");

		_errorIndexName = _settings.ErrorIndexName;
		var retry = options.Value.Projections?.RetryPolicy ?? new ProjectionRetryOptions();
		_indexRetry = ProjectionErrorPersistenceRetry.Create(retry, retry.MaxIndexAttempts, _logger, "create");
		_bulkRetry = ProjectionErrorPersistenceRetry.Create(retry, retry.MaxBulkAttempts, _logger, "bulk create");
	}

	/// <inheritdoc />
	public async Task HandleProjectionErrorAsync(ProjectionErrorContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		cancellationToken.ThrowIfCancellationRequested();

		using var activity = Activity.Current?.Source.StartActivity("HandleProjectionError");
		_ = (activity?.SetTag("projection.type", context.ProjectionType));
		_ = (activity?.SetTag("operation.type", context.OperationType));
		_ = (activity?.SetTag("document.id", context.DocumentId));

		var stopwatch = ValueStopwatch.StartNew();

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
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
		cancellationToken.ThrowIfCancellationRequested();

		using var activity = Activity.Current?.Source.StartActivity("HandleBulkOperationErrors");
		_ = (activity?.SetTag("projection.type", context.ProjectionType));
		_ = (activity?.SetTag("operation.type", context.OperationType));
		_ = (activity?.SetTag("total_documents", context.TotalDocuments));
		_ = (activity?.SetTag("failed_documents", context.Failures.Count));

		var stopwatch = ValueStopwatch.StartNew();

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
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			var searchRequest = new SearchRequest(_errorIndexName)
			{
				Size = maxResults,
				Query = BuildErrorQuery(fromDate, toDate, projectionType),
				Sort = [new SortOptions { Field = new FieldSort("timestamp") { Order = SortOrder.Desc } }],
			};

			var response = await _client.SearchAsync<ProjectionErrorRecord>(searchRequest, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			if (!response.IsValidResponse || response.Documents == null)
			{
				var errorMessage = response.ApiCallDetails?.OriginalException?.Message ?? "Unknown error";
				_logger.LogWarning("Failed to retrieve projection error records: {Error}", errorMessage);
				return [];
			}

			return response.Documents;
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			_logger.LogError(ex, "Error retrieving projection error records");
			return [];
		}
	}

	/// <inheritdoc />
	public async Task<int> MarkErrorsAsResolvedAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(errorIds);
		cancellationToken.ThrowIfCancellationRequested();

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
						Should = [.. errorIdList.Select(id => (Query)new TermQuery { Field = "id", Value = id })],
					},
				Script = new Script
				{
					Source = "ctx._source.isResolved = true; ctx._source.resolvedAt = params.now",
					Params = new Dictionary<string, object>(StringComparer.Ordinal) { ["now"] = DateTimeOffset.UtcNow },
				},
			};

			var response = await _client.UpdateByQueryAsync(updateByQueryRequest, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

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
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
		var queries = new List<Query> { new DateRangeQuery("timestamp") { Gte = fromDate, Lte = toDate } };

		if (!string.IsNullOrWhiteSpace(projectionType))
		{
			queries.Add(new TermQuery { Field = "projectionType", Value = projectionType });
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
			var writeIndex = await ResolveErrorWriteIndexAsync(cancellationToken).ConfigureAwait(false);

			var request = new CreateRequest<ProjectionErrorRecord>(record, writeIndex.Index, record.Id)
			{
				Routing = writeIndex.Routing is null ? null : new Routing(writeIndex.Routing),
			};
			CreateResponse? response = null;
			_ = await _indexRetry.ExecuteAsync(async token =>
			{
				request.RequestConfiguration = CreateSingleAttemptConfiguration();
				response = await _client.CreateAsync(request, token).ConfigureAwait(false);
				token.ThrowIfCancellationRequested();
				return ProjectionErrorPersistenceRetry.IsUnsettledCreateOutcome(response.ApiCallDetails?.HttpStatusCode);
			}, cancellationToken).ConfigureAwait(false);

			if (response is { IsValidResponse: false })
			{
				_logger.LogWarning(
					"Unable to confirm storage of projection error record: {Error}",
					response.ApiCallDetails?.OriginalException?.Message ?? "Unknown error");
			}
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
			var writeIndex = await ResolveErrorWriteIndexAsync(cancellationToken).ConfigureAwait(false);

			var pending = records.ToList();
			var permanentFailures = 0;
			_ = await _bulkRetry.ExecuteAsync(async token =>
			{
				var request = new BulkRequest(writeIndex.Index)
				{
					Routing = writeIndex.Routing is null ? null : new Routing(writeIndex.Routing),
					Operations = [.. pending.Select(static record => (IBulkOperation)new BulkCreateOperation<ProjectionErrorRecord>(record) { Id = record.Id })],
					RequestConfiguration = CreateSingleAttemptConfiguration(),
				};
				var response = await _client.BulkAsync(request, token).ConfigureAwait(false);
				token.ThrowIfCancellationRequested();
				if (response.Items is null || response.Items.Count == 0)
				{
					// No interpretable item list, so this envelope says nothing about any record in it --
					// including a 200, which is the correct status for a HEALTHY bulk response and is only
					// uninterpretable because of the condition just tested. `pending` is untouched on this
					// path, so it is still the full set or the last set a response validated; replaying it
					// is not an arbitrary subset.
					return ProjectionErrorPersistenceRetry.IsUnsettledBulkEnvelope(response.ApiCallDetails?.HttpStatusCode);
				}

				// A malformed response supplies no authority to replay an arbitrary subset -- which is what
				// the shape check below establishes. The envelope's STATUS is deliberately not consulted
				// here: a status-based guard stood at this line enumerating which codes were transient and
				// treating the rest as a verdict, so a 500, 507 or 408 arriving alongside a usable item
				// list ended the loop and discarded the batch. Whether Elasticsearch answered is decided
				// per item below, where the answer actually lives.
				var expected = pending.ToDictionary(static record => record.Id, StringComparer.Ordinal);

				// ATTRIBUTION, and it is terminal. A response carrying an id we never sent, an operation we
				// never issued, or the same id twice is not a partial answer -- it is an incoherent one, and
				// incoherence is a fact about the RESPONDER, not about our records. It takes a rewriting
				// proxy, a wrong endpoint or a version mismatch to produce, and no number of replays
				// changes who answered. Note the duplicate test is against this RESPONSE's own count, not
				// against ours: measured against `expected.Count` it would also catch mere incompleteness
				// and route it here, which would silently defeat the branch below.
				if (response.Items.Any(item => item.Id is null || !expected.ContainsKey(item.Id) || !string.Equals(item.Operation, "create", StringComparison.Ordinal)) ||
					response.Items.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count() != response.Items.Count)
				{
					return false;
				}

				// COMPLETENESS, and it is a replay. Every id present is ours and distinct, there are simply
				// fewer than we sent. That is an ABSENCE of evidence about the missing records -- the same
				// physical event as a truncated response, one degree less severe, because loss OMITS and
				// cannot INVENT. `pending` is untouched here, so the whole validated set is replayed and
				// nothing is selected on no evidence.
				if (response.Items.Count != expected.Count)
				{
					return true;
				}

				// Every id here is one of ours, present exactly once -- the shape check above establishes
				// that, and it is what makes choosing a replay subset legitimate rather than a guess.
				// Only a verdict removes a record from the set; an item we could not interpret is replayed,
				// because the alternative is discarding it on no evidence. Replaying more creates cannot
				// lose a record, and the attempt budget bounds the loop whatever its size.
				var retryable = new List<ProjectionErrorRecord>();
				foreach (var item in response.Items)
				{
					switch (ProjectionErrorPersistenceRetry.ClassifyBulkItem(item.Status, item.Error is not null))
					{
						case ProjectionErrorPersistenceRetry.BulkItemOutcome.Stored:
							break;
						case ProjectionErrorPersistenceRetry.BulkItemOutcome.Rejected:
							permanentFailures++;
							break;
						default:
							retryable.Add(expected[item.Id!]);
							break;
					}
				}
				pending = retryable;
				return pending.Count > 0;
			}, cancellationToken).ConfigureAwait(false);

			var failedCount = permanentFailures + pending.Count;
			if (failedCount > 0)
			{
				_logger.LogWarning("Unable to confirm storage of {FailedCount}/{TotalCount} projection error records after the bounded persistence budget",
					failedCount, records.Count);
			}

		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			_logger.LogError(ex, "Error storing bulk projection error records");
		}
	}

	private sealed record ErrorWriteTarget(string Index, string? Routing);

	private async Task<ErrorWriteTarget> ResolveErrorWriteIndexAsync(CancellationToken cancellationToken)
	{
		var response = await _client.Indices.GetAsync(new GetIndexRequest(_errorIndexName)
		{
			RequestConfiguration = CreateSingleAttemptConfiguration(),
		}, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		if (!response.IsValidResponse || response.Indices.Count == 0)
		{
			throw new InvalidOperationException("Cannot resolve projection error persistence to a concrete index; view_index_metadata permission is required.");
		}

		// Data streams and wildcard targets require additional authority. Never guess.
		if (response.Indices.Any(static index => !string.IsNullOrEmpty(index.Value.DataStream)))
		{
			throw new InvalidOperationException("Projection error persistence requires an index or index alias, not a data stream.");
		}
		if (response.Indices.Count == 1 && response.Indices.TryGetValue(_errorIndexName, out _))
		{
			return new ErrorWriteTarget(_errorIndexName, null);
		}

		var aliases = response.Indices.Where(index => index.Value.Aliases.ContainsKey(_errorIndexName)).ToArray();
		var writable = aliases.Where(index => index.Value.Aliases[_errorIndexName].IsWriteIndex == true).ToArray();
		var target = writable.Length == 1 ? writable[0] : aliases.Length == 1 && aliases[0].Value.Aliases[_errorIndexName].IsWriteIndex != false ? aliases[0] : default;
		if (target.Key is null || aliases.Length != response.Indices.Count)
		{
			throw new InvalidOperationException("Projection error alias must have one unambiguous write index.");
		}
		var alias = target.Value.Aliases[_errorIndexName];
		return new ErrorWriteTarget(target.Key, alias.IndexRouting ?? alias.Routing);
	}

	private RequestConfiguration CreateSingleAttemptConfiguration()
	{
		var settings = _client.ElasticsearchClientSettings;
		if (settings.ForceNode is { } forcedNode)
		{
			return new RequestConfiguration { ForceNode = forcedNode };
		}
		var predicate = settings.NodePredicate ?? settings.ProductRegistration.NodePredicate;
		Uri? selected = null;
		// Consume the SDK's view to completion; it owns rotation, health selection and iterator state.
		foreach (var node in settings.NodePool.CreateView())
		{
			if (selected is null && predicate(node))
			{
				selected = node.Uri;
			}
		}
		if (selected is null)
		{
			throw new InvalidOperationException("No eligible Elasticsearch node is available for projection error persistence.");
		}
		// Elastic.Transport 0.17.1 ignores request-local MaxRetries; ForceNode is its documented no-retry path.
		return new RequestConfiguration { ForceNode = selected };
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
