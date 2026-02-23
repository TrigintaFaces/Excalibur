// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Reindex;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.IndexManagement;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Manages projection rebuild operations for ElasticSearch-backed projections.
/// </summary>
public sealed class ProjectionRebuildManager : IProjectionRebuildManager
{
	private readonly ElasticsearchClient _client;
	private readonly IIndexAliasManager _aliasManager;
	private readonly ProjectionOptions _settings;
	private readonly ILogger<ProjectionRebuildManager> _logger;
	private readonly string _operationsIndexName;

	private readonly ConcurrentDictionary<string, CancellationTokenSource> _operationTokens =
		new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionRebuildManager" /> class.
	/// </summary>
	/// <param name="client"> The Elasticsearch client. </param>
	/// <param name="aliasManager"> The alias manager for zero-downtime swaps. </param>
	/// <param name="options"> Projection settings. </param>
	/// <param name="logger"> Logger instance. </param>
	public ProjectionRebuildManager(
		ElasticsearchClient client,
		IIndexAliasManager aliasManager,
		IOptions<ProjectionOptions> options,
		ILogger<ProjectionRebuildManager> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_aliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
		ArgumentNullException.ThrowIfNull(options);
		_settings = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_operationsIndexName = $"{_settings.IndexPrefix}-rebuild-operations";
	}

	/// <inheritdoc />
	public async Task<ProjectionRebuildResult> StartRebuildAsync(
		ProjectionRebuildRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectionType);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetIndexName);

		var startedAt = DateTimeOffset.UtcNow;
		var operationId = Guid.NewGuid().ToString("N");

		if (!_settings.RebuildManager.Enabled)
		{
			return new ProjectionRebuildResult
			{
				OperationId = operationId,
				Started = false,
				Message = "Projection rebuilds are disabled by configuration.",
				StartedAt = startedAt,
			};
		}

		var useAliasing = request.UseAliasing && _settings.RebuildManager.UseAliasing;
		var actualTargetIndex = useAliasing && request.CreateNewIndex
			? $"{request.TargetIndexName}-{startedAt:yyyyMMddHHmmss}-{operationId[..8]}"
			: request.TargetIndexName;

		await EnsureOperationsIndexExistsAsync(cancellationToken).ConfigureAwait(false);

		if (request.CreateNewIndex &&
			!await EnsureTargetIndexAsync(actualTargetIndex, cancellationToken)
				.ConfigureAwait(false))
		{
			var failedStatus = CreateOperationDocument(
				operationId,
				request,
				startedAt,
				actualTargetIndex,
				RebuildState.Failed,
				completedAt: DateTimeOffset.UtcNow,
				lastError: "Target index creation failed.");

			await StoreOperationAsync(failedStatus, cancellationToken).ConfigureAwait(false);

			return new ProjectionRebuildResult
			{
				OperationId = operationId,
				Started = false,
				Message = $"Failed to create index '{actualTargetIndex}'.",
				StartedAt = startedAt,
			};
		}

		var totalDocuments = await GetSourceDocumentCountAsync(request.SourceIndexName, cancellationToken)
			.ConfigureAwait(false);

		var statusDocument = CreateOperationDocument(
			operationId,
			request,
			startedAt,
			actualTargetIndex,
			RebuildState.InProgress,
			totalDocuments: totalDocuments,
			processedDocuments: 0,
			failedDocuments: 0);

		await StoreOperationAsync(statusDocument, cancellationToken).ConfigureAwait(false);

		var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_operationTokens[operationId] = tokenSource;

		_ = Task.Factory.StartNew(
				() => ExecuteRebuildAsync(
					operationId,
					request,
					actualTargetIndex,
					useAliasing,
					totalDocuments,
					startedAt,
					tokenSource.Token),
				CancellationToken.None,
				TaskCreationOptions.DenyChildAttach,
				TaskScheduler.Default)
			.Unwrap();

		return new ProjectionRebuildResult { OperationId = operationId, Started = true, StartedAt = startedAt, };
	}

	/// <inheritdoc />
	public async Task<ProjectionRebuildStatus> GetRebuildStatusAsync(
		string operationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

		var document = await GetOperationAsync(operationId, cancellationToken).ConfigureAwait(false);
		if (document is null)
		{
			return new ProjectionRebuildStatus
			{
				OperationId = operationId,
				State = RebuildState.Failed,
				ProjectionType = "Unknown",
				StartedAt = DateTimeOffset.UtcNow,
			};
		}

		return ToStatus(document);
	}

	/// <inheritdoc />
	public async Task<bool> CancelRebuildAsync(
		string operationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

		if (_operationTokens.TryRemove(operationId, out var tokenSource))
		{
			await tokenSource.CancelAsync().ConfigureAwait(false);
			tokenSource.Dispose();
		}

		var existing = await GetOperationAsync(operationId, cancellationToken).ConfigureAwait(false);
		if (existing is null)
		{
			return false;
		}

		if (existing.State is RebuildState.Completed or RebuildState.Failed)
		{
			return true;
		}

		var cancelled = existing with
		{
			State = RebuildState.Cancelled,
			CompletedAt = DateTimeOffset.UtcNow,
			PercentComplete = existing.PercentComplete,
		};

		await StoreOperationAsync(cancelled, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<ProjectionRebuildSummary>> ListRebuildOperationsAsync(
		DateTime fromDate,
		DateTime toDate,
		string? projectionType,
		CancellationToken cancellationToken)
	{
		await EnsureOperationsIndexExistsAsync(cancellationToken).ConfigureAwait(false);

		var queries = new List<Query> { new DateRangeQuery(new Field("startedAt")) { Gte = fromDate, Lte = toDate, }, };

		if (!string.IsNullOrWhiteSpace(projectionType))
		{
			queries.Add(new TermQuery(new Field("projectionType")) { Value = projectionType, });
		}

		var request = new SearchRequest(_operationsIndexName)
		{
			Size = 100,
			Query = queries.Count == 1 ? queries[0] : new BoolQuery { Must = queries.ToArray() },
			Sort = new List<SortOptions> { SortOptions.Field(new Field("startedAt"), new FieldSort { Order = SortOrder.Desc }), },
		};

		var response = await _client.SearchAsync<ProjectionRebuildOperationDocument>(
				request,
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Documents is null)
		{
			_logger.LogWarning(
				"Failed to list projection rebuild operations: {Error}",
				response.DebugInformation);
			return [];
		}

		return response.Documents.Select(ToSummary);
	}

	/// <inheritdoc />
	public async Task<ProjectionRebuildValidation> ValidateRebuildAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

		if (!_settings.RebuildManager.Enabled)
		{
			return new ProjectionRebuildValidation
			{
				CanRebuild = false,
				ValidationMessages = ["Projection rebuilds are disabled by configuration."],
				HasSufficientResources = false,
			};
		}

		var projectionIndex = $"{_settings.IndexPrefix}-{projectionType.ToLowerInvariant()}";
		var existsResponse = await _client.Indices.ExistsAsync(projectionIndex, cancellationToken)
			.ConfigureAwait(false);

		var messages = new List<string>();
		var warnings = new List<string>();
		long? estimatedDocuments = null;

		if (!existsResponse.Exists)
		{
			warnings.Add($"Index '{projectionIndex}' does not exist. A rebuild will create a new index.");
		}
		else
		{
			var countResponse = await _client.CountAsync<object>(
					c => c.Indices(projectionIndex),
					cancellationToken)
				.ConfigureAwait(false);
			if (countResponse.IsValidResponse)
			{
				estimatedDocuments = countResponse.Count;
			}
		}

		return new ProjectionRebuildValidation
		{
			CanRebuild = true,
			ValidationMessages = messages,
			Warnings = warnings,
			EstimatedDocumentCount = estimatedDocuments,
			HasSufficientResources = true,
		};
	}

	private static ProjectionRebuildStatus ToStatus(ProjectionRebuildOperationDocument document)
	{
		return new ProjectionRebuildStatus
		{
			OperationId = document.OperationId,
			ProjectionType = document.ProjectionType,
			State = document.State,
			StartedAt = document.StartedAt,
			CompletedAt = document.CompletedAt,
			TotalDocuments = document.TotalDocuments,
			ProcessedDocuments = document.ProcessedDocuments,
			FailedDocuments = document.FailedDocuments,
			PercentComplete = document.PercentComplete,
			DocumentsPerSecond = document.DocumentsPerSecond,
			EstimatedTimeRemaining = document.EstimatedTimeRemainingMs is null
				? null
				: TimeSpan.FromMilliseconds(document.EstimatedTimeRemainingMs.Value),
			LastError = document.LastError,
			Checkpoint = document.Checkpoint,
		};
	}

	private static ProjectionRebuildSummary ToSummary(ProjectionRebuildOperationDocument document)
	{
		var duration = document.CompletedAt.HasValue
			? (TimeSpan?)(document.CompletedAt.Value - document.StartedAt)
			: null;

		return new ProjectionRebuildSummary
		{
			OperationId = document.OperationId,
			ProjectionType = document.ProjectionType,
			State = document.State,
			StartedAt = document.StartedAt,
			CompletedAt = document.CompletedAt,
			ProcessedDocuments = document.ProcessedDocuments,
			FailedDocuments = document.FailedDocuments,
			Duration = duration,
		};
	}

	private static ProjectionRebuildOperationDocument CreateOperationDocument(
		string operationId,
		ProjectionRebuildRequest request,
		DateTimeOffset startedAt,
		string targetIndexName,
		RebuildState state,
		long totalDocuments = 0,
		long processedDocuments = 0,
		long failedDocuments = 0,
		DateTimeOffset? completedAt = null,
		double percentComplete = 0,
		double documentsPerSecond = 0,
		double? estimatedTimeRemainingMs = null,
		string? lastError = null)
	{
		return new ProjectionRebuildOperationDocument
		{
			OperationId = operationId,
			ProjectionType = request.ProjectionType,
			TargetIndexName = targetIndexName,
			SourceIndexName = request.SourceIndexName,
			State = state,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			TotalDocuments = totalDocuments,
			ProcessedDocuments = processedDocuments,
			FailedDocuments = failedDocuments,
			PercentComplete = percentComplete,
			DocumentsPerSecond = documentsPerSecond,
			EstimatedTimeRemainingMs = estimatedTimeRemainingMs,
			LastError = lastError,
			Metadata = request.Metadata,
		};
	}

	private async Task ExecuteRebuildAsync(
		string operationId,
		ProjectionRebuildRequest request,
		string actualTargetIndex,
		bool useAliasing,
		long totalDocuments,
		DateTimeOffset startedAt,
		CancellationToken cancellationToken)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!string.IsNullOrWhiteSpace(request.SourceIndexName))
			{
				var reindexRequest = new ReindexRequest
				{
					Source = new Source { Indices = request.SourceIndexName, },
					Dest = new Destination { Index = actualTargetIndex, },
					Refresh = true,
					WaitForCompletion = true,
				};

				var reindexResponse = await _client.ReindexAsync(
						reindexRequest,
						cancellationToken)
					.ConfigureAwait(false);

				if (!reindexResponse.IsValidResponse)
				{
					throw new InvalidOperationException(
						$"Reindex request failed: {reindexResponse.DebugInformation}");
				}
			}

			if (useAliasing)
			{
				await SwitchAliasAsync(
						request.TargetIndexName,
						actualTargetIndex,
						cancellationToken)
					.ConfigureAwait(false);
			}

			var completedAt = DateTimeOffset.UtcNow;
			var processedDocuments = await GetSourceDocumentCountAsync(
					actualTargetIndex,
					cancellationToken)
				.ConfigureAwait(false);

			var durationSeconds = Math.Max(1, (completedAt - startedAt).TotalSeconds);
			var documentsPerSecond = processedDocuments / durationSeconds;

			var completed = CreateOperationDocument(
				operationId,
				request,
				startedAt,
				actualTargetIndex,
				RebuildState.Completed,
				totalDocuments: totalDocuments,
				processedDocuments: processedDocuments,
				failedDocuments: 0,
				completedAt: completedAt,
				percentComplete: 100,
				documentsPerSecond: documentsPerSecond,
				estimatedTimeRemainingMs: 0);

			await StoreOperationAsync(completed, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			var cancelled = CreateOperationDocument(
				operationId,
				request,
				startedAt,
				actualTargetIndex,
				RebuildState.Cancelled,
				totalDocuments: totalDocuments,
				processedDocuments: 0,
				failedDocuments: 0,
				completedAt: DateTimeOffset.UtcNow,
				lastError: "Rebuild cancelled.");

			await StoreOperationAsync(cancelled, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Projection rebuild failed for {ProjectionType}",
				request.ProjectionType);

			var failed = CreateOperationDocument(
				operationId,
				request,
				startedAt,
				actualTargetIndex,
				RebuildState.Failed,
				totalDocuments: totalDocuments,
				processedDocuments: 0,
				failedDocuments: 0,
				completedAt: DateTimeOffset.UtcNow,
				lastError: ex.Message);

			await StoreOperationAsync(failed, CancellationToken.None).ConfigureAwait(false);
		}
		finally
		{
			if (_operationTokens.TryRemove(operationId, out var tokenSource))
			{
				tokenSource.Dispose();
			}
		}
	}

	private async Task<bool> EnsureTargetIndexAsync(string indexName, CancellationToken cancellationToken)
	{
		var existsResponse = await _client.Indices.ExistsAsync(indexName, cancellationToken)
			.ConfigureAwait(false);
		if (existsResponse.Exists)
		{
			return true;
		}

		var createRequest = new CreateIndexRequest(indexName)
		{
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0, },
		};

		var createResponse = await _client.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!createResponse.IsValidResponse)
		{
			_logger.LogError(
				"Failed to create target index {IndexName}: {Error}",
				indexName,
				createResponse.DebugInformation);
			return false;
		}

		return true;
	}

	private async Task SwitchAliasAsync(
		string aliasName,
		string targetIndexName,
		CancellationToken cancellationToken)
	{
		var existingAliases = await _aliasManager.GetAliasesAsync(aliasName, cancellationToken)
			.ConfigureAwait(false);

		var operations = new List<AliasOperation>();
		foreach (var alias in existingAliases)
		{
			foreach (var index in alias.Indices)
			{
				operations.Add(new AliasOperation { AliasName = aliasName, IndexName = index, OperationType = AliasOperationType.Remove, });
			}
		}

		operations.Add(new AliasOperation
		{
			AliasName = aliasName,
			IndexName = targetIndexName,
			OperationType = AliasOperationType.Add,
			AliasConfiguration = new Alias { IsWriteIndex = true },
		});

		if (operations.Count == 1)
		{
			_ = await _aliasManager.CreateAliasAsync(
					aliasName,
					[targetIndexName],
					new Alias { IsWriteIndex = true },
					cancellationToken)
				.ConfigureAwait(false);
			return;
		}

		var success = await _aliasManager.UpdateAliasesAsync(operations, cancellationToken)
			.ConfigureAwait(false);

		if (!success)
		{
			throw new InvalidOperationException($"Failed to switch alias '{aliasName}'.");
		}
	}

	private async Task<long> GetSourceDocumentCountAsync(
		string? indexName,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(indexName))
		{
			return 0;
		}

		var response = await _client.CountAsync<object>(
				c => c.Indices(indexName),
				cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse ? response.Count : 0;
	}

	private async Task<ProjectionRebuildOperationDocument?> GetOperationAsync(
		string operationId,
		CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync<ProjectionRebuildOperationDocument>(
				_operationsIndexName,
				operationId,
				cancellationToken)
			.ConfigureAwait(false);

		return response is { IsValidResponse: true, Found: true }
			? response.Source
			: null;
	}

	private async Task StoreOperationAsync(
		ProjectionRebuildOperationDocument document,
		CancellationToken cancellationToken)
	{
		var response = await _client.IndexAsync(
				document,
				idx => idx.Index(_operationsIndexName).Id(document.OperationId),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to store projection rebuild status for {OperationId}: {Error}",
				document.OperationId,
				response.DebugInformation);
		}

		_ = await _client.Indices.RefreshAsync(_operationsIndexName, cancellationToken).ConfigureAwait(false);
	}

	private async Task EnsureOperationsIndexExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client.Indices.ExistsAsync(_operationsIndexName, cancellationToken)
			.ConfigureAwait(false);
		if (existsResponse.Exists)
		{
			return;
		}

		var createRequest = new CreateIndexRequest(_operationsIndexName)
		{
			Mappings = new TypeMapping
			{
				Properties = new Properties
				{
					["operationId"] = new KeywordProperty(),
					["projectionType"] = new KeywordProperty(),
					["targetIndexName"] = new KeywordProperty(),
					["sourceIndexName"] = new KeywordProperty(),
					["state"] = new KeywordProperty(),
					["startedAt"] = new DateProperty(),
					["completedAt"] = new DateProperty(),
					["totalDocuments"] = new LongNumberProperty(),
					["processedDocuments"] = new LongNumberProperty(),
					["failedDocuments"] = new LongNumberProperty(),
					["percentComplete"] = new DoubleNumberProperty(),
					["documentsPerSecond"] = new DoubleNumberProperty(),
					["estimatedTimeRemainingMs"] = new DoubleNumberProperty(),
					["lastError"] = new TextProperty { Fields = new Properties { ["keyword"] = new KeywordProperty() }, },
					["checkpoint"] = new KeywordProperty(),
					["metadata"] = new ObjectProperty(),
				},
			},
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0, },
		};

		var response = await _client.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to create rebuild operations index {IndexName}: {Error}",
				_operationsIndexName,
				response.DebugInformation);
		}
	}

	private sealed record ProjectionRebuildOperationDocument
	{
		public required string OperationId { get; init; }
		public required string ProjectionType { get; init; }
		public required string TargetIndexName { get; init; }
		public string? SourceIndexName { get; init; }
		public required RebuildState State { get; init; }
		public required DateTimeOffset StartedAt { get; init; }
		public DateTimeOffset? CompletedAt { get; init; }
		public long TotalDocuments { get; init; }
		public long ProcessedDocuments { get; init; }
		public long FailedDocuments { get; init; }
		public double PercentComplete { get; init; }
		public double DocumentsPerSecond { get; init; }
		public double? EstimatedTimeRemainingMs { get; init; }
		public string? LastError { get; init; }
		public string? Checkpoint { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
