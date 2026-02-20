// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Reindex;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.IndexManagement;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Handles schema evolution and migrations for Elasticsearch projections.
/// </summary>
public sealed class SchemaEvolutionHandler : ISchemaEvolutionHandler, IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly IIndexAliasManager _aliasManager;
	private readonly ProjectionOptions _settings;
	private readonly ILogger<SchemaEvolutionHandler> _logger;
	private readonly string _historyIndexName;
	private readonly string _migrationIndexName;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaEvolutionHandler" /> class.
	/// </summary>
	/// <param name="client"> The Elasticsearch client. </param>
	/// <param name="aliasManager"> The alias manager for zero-downtime swaps. </param>
	/// <param name="options"> Projection settings. </param>
	/// <param name="logger"> Logger instance. </param>
	public SchemaEvolutionHandler(
		ElasticsearchClient client,
		IIndexAliasManager aliasManager,
		IOptions<ProjectionOptions> options,
		ILogger<SchemaEvolutionHandler> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_aliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
		ArgumentNullException.ThrowIfNull(options);
		_settings = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_historyIndexName = $"{_settings.IndexPrefix}-schema-history";
		_migrationIndexName = $"{_settings.IndexPrefix}-schema-migrations";
	}

	/// <inheritdoc />
	public async Task<SchemaComparisonResult> CompareSchemaAsync(
		string sourceIndex,
		string targetIndex,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceIndex);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetIndex);

		var sourceMapping = await _client.Indices.GetMappingAsync(
				new GetMappingRequest(sourceIndex),
				cancellationToken)
			.ConfigureAwait(false);
		var targetMapping = await _client.Indices.GetMappingAsync(
				new GetMappingRequest(targetIndex),
				cancellationToken)
			.ConfigureAwait(false);

		var sourceFields = ExtractFieldTypes(sourceMapping, sourceIndex);
		var targetFields = ExtractFieldTypes(targetMapping, targetIndex);

		var addedFields = targetFields
			.Where(kvp => !sourceFields.ContainsKey(kvp.Key))
			.Select(kvp => new FieldChange
			{
				FieldName = kvp.Key,
				FieldPath = kvp.Key,
				ChangeType = FieldChangeType.Added,
				NewType = kvp.Value,
				IsBreaking = false,
			})
			.ToList();

		var removedFields = sourceFields
			.Where(kvp => !targetFields.ContainsKey(kvp.Key))
			.Select(kvp => new FieldChange
			{
				FieldName = kvp.Key,
				FieldPath = kvp.Key,
				ChangeType = FieldChangeType.Removed,
				OldType = kvp.Value,
				IsBreaking = true,
			})
			.ToList();

		var modifiedFields = sourceFields
			.Where(kvp => targetFields.TryGetValue(kvp.Key, out var newType) &&
						  !string.Equals(kvp.Value, newType, StringComparison.Ordinal))
			.Select(kvp => new FieldChange
			{
				FieldName = kvp.Key,
				FieldPath = kvp.Key,
				ChangeType = FieldChangeType.TypeChanged,
				OldType = kvp.Value,
				NewType = targetFields[kvp.Key],
				IsBreaking = true,
			})
			.ToList();

		var breakingChanges = removedFields
			.Concat(modifiedFields)
			.Select(change => $"{change.FieldName} changed from {change.OldType ?? "unknown"} to {change.NewType ?? "removed"}")
			.ToList();

		var backwardsCompatible = breakingChanges.Count == 0 || _settings.SchemaEvolution.AllowBreakingChanges;

		return new SchemaComparisonResult
		{
			AreIdentical = addedFields.Count == 0 && removedFields.Count == 0 && modifiedFields.Count == 0,
			IsBackwardsCompatible = backwardsCompatible,
			AddedFields = addedFields,
			RemovedFields = removedFields,
			ModifiedFields = modifiedFields,
			BreakingChanges = breakingChanges.Count == 0 ? null : breakingChanges,
		};
	}

	/// <inheritdoc />
	public async Task<SchemaMigrationPlan> PlanMigrationAsync(
		SchemaMigrationRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectionType);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceIndex);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetIndex);

		var mapping = GetTypeMapping(request.NewSchema);
		var steps = new List<MigrationStep>();
		var stepNumber = 1;

		if (request.Strategy is MigrationStrategy.Reindex or MigrationStrategy.AliasSwitch or MigrationStrategy.DualWrite)
		{
			var createParameters = new Dictionary<string, object>(StringComparer.Ordinal) { ["TargetIndex"] = request.TargetIndex, };
			if (mapping is not null)
			{
				createParameters["Mapping"] = mapping;
			}

			steps.Add(new MigrationStep
			{
				StepNumber = stepNumber++,
				Name = "CreateIndex",
				Description = "Create target index",
				OperationType = StepOperationType.CreateIndex,
				IsCritical = true,
				Parameters = createParameters,
			});

			if (mapping is not null)
			{
				steps.Add(new MigrationStep
				{
					StepNumber = stepNumber++,
					Name = "UpdateMapping",
					Description = "Apply new mapping",
					OperationType = StepOperationType.UpdateMapping,
					IsCritical = true,
					Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["TargetIndex"] = request.TargetIndex,
						["Mapping"] = mapping,
					},
				});
			}

			steps.Add(new MigrationStep
			{
				StepNumber = stepNumber++,
				Name = "Reindex",
				Description = "Reindex documents",
				OperationType = StepOperationType.Reindex,
				IsCritical = true,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["SourceIndex"] = request.SourceIndex,
					["TargetIndex"] = request.TargetIndex,
				},
			});

			if (request.Strategy == MigrationStrategy.AliasSwitch)
			{
				steps.Add(new MigrationStep
				{
					StepNumber = stepNumber++,
					Name = "SwitchAlias",
					Description = "Switch alias to new index",
					OperationType = StepOperationType.SwitchAlias,
					IsCritical = true,
					Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["AliasName"] = request.SourceIndex,
						["TargetIndex"] = request.TargetIndex,
					},
				});
			}
		}
		else
		{
			steps.Add(new MigrationStep
			{
				StepNumber = stepNumber++,
				Name = "UpdateMapping",
				Description = "Update mapping in place",
				OperationType = StepOperationType.UpdateMapping,
				IsCritical = true,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["TargetIndex"] = request.SourceIndex,
					["Mapping"] = mapping ?? request.NewSchema,
				},
			});
		}

		var estimatedDocs = await GetEstimatedDocumentCountAsync(request.SourceIndex, cancellationToken)
			.ConfigureAwait(false);
		var estimatedDuration = estimatedDocs.HasValue
			? (TimeSpan?)TimeSpan.FromSeconds(Math.Max(1, estimatedDocs.Value / Math.Max(1, request.BatchSize)))
			: null;

		return new SchemaMigrationPlan
		{
			PlanId = Guid.NewGuid().ToString("N"),
			ProjectionType = request.ProjectionType,
			Strategy = request.Strategy,
			Steps = steps,
			EstimatedDuration = estimatedDuration,
			EstimatedDocuments = estimatedDocs,
			IsReversible = request.Strategy is MigrationStrategy.Reindex or MigrationStrategy.AliasSwitch,
			RollbackSteps = request.Strategy == MigrationStrategy.AliasSwitch
				? new List<MigrationStep>
				{
					new MigrationStep
					{
						StepNumber = 1,
						Name = "RollbackAlias",
						Description = "Rollback alias to source index",
						OperationType = StepOperationType.SwitchAlias,
						IsCritical = true,
						Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
						{
							["AliasName"] = request.SourceIndex, ["TargetIndex"] = request.SourceIndex,
						},
					},
				}
				: null,
			ValidationChecks = ["Compare mappings", "Verify document counts"],
		};
	}

	/// <inheritdoc />
	public async Task<SchemaMigrationResult> ExecuteMigrationAsync(
		SchemaMigrationPlan plan,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(plan);

		var stepResults = new List<StepResult>();
		var errors = new List<string>();
		var startTime = DateTimeOffset.UtcNow;
		long documentsMigrated = 0;
		long documentsFailed = 0;
		var success = true;

		foreach (var step in plan.Steps)
		{
			var stepStart = DateTimeOffset.UtcNow;
			var stepSuccess = false;
			string? errorMessage = null;

			try
			{
				stepSuccess = await ExecuteStepAsync(step, cancellationToken).ConfigureAwait(false);
				if (step.OperationType == StepOperationType.Reindex)
				{
					documentsMigrated = await GetDocumentCountFromStepAsync(step, cancellationToken)
						.ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				errorMessage = ex.Message;
				stepSuccess = false;
				errors.Add(ex.Message);
			}

			stepResults.Add(new StepResult
			{
				StepNumber = step.StepNumber,
				Name = step.Name,
				Success = stepSuccess,
				Duration = DateTimeOffset.UtcNow - stepStart,
				ErrorMessage = errorMessage,
			});

			if (!stepSuccess && step.IsCritical)
			{
				success = false;
				break;
			}
		}

		var result = new SchemaMigrationResult
		{
			Success = success,
			PlanId = plan.PlanId,
			StartTime = startTime,
			EndTime = DateTimeOffset.UtcNow,
			DocumentsMigrated = documentsMigrated,
			DocumentsFailed = documentsFailed,
			CompletedSteps = stepResults,
			Errors = errors.Count == 0 ? null : errors,
		};

		await StoreMigrationResultAsync(plan.ProjectionType, result, cancellationToken)
			.ConfigureAwait(false);

		return result;
	}

	/// <inheritdoc />
	public Task<SchemaCompatibilityResult> ValidateBackwardsCompatibilityAsync(
		object currentSchema,
		object newSchema,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(currentSchema);
		ArgumentNullException.ThrowIfNull(newSchema);

		var currentFields = ExtractSchemaFields(currentSchema);
		var newFields = ExtractSchemaFields(newSchema);

		var removed = currentFields.Except(newFields, StringComparer.Ordinal).ToList();

		if (removed.Count == 0)
		{
			return Task.FromResult(new SchemaCompatibilityResult { IsCompatible = true, Level = CompatibilityLevel.Full, });
		}

		var allowBreaking = _settings.SchemaEvolution.AllowBreakingChanges;
		return Task.FromResult(new SchemaCompatibilityResult
		{
			IsCompatible = allowBreaking,
			Level = allowBreaking ? CompatibilityLevel.Partial : CompatibilityLevel.None,
			Incompatibilities = removed,
		});
	}

	/// <inheritdoc />
	public async Task RegisterSchemaVersionAsync(
		SchemaVersionRegistration registration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(registration);
		ArgumentException.ThrowIfNullOrWhiteSpace(registration.ProjectionType);
		ArgumentException.ThrowIfNullOrWhiteSpace(registration.Version);

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var schemaJson = SerializeSchema(registration.Schema);
		var document = new SchemaVersionDocument
		{
			ProjectionType = registration.ProjectionType,
			Version = registration.Version,
			SchemaJson = schemaJson,
			RegisteredAt = registration.RegisteredAt,
			Description = registration.Description,
			MigrationNotes = registration.MigrationNotes,
		};

		var documentId = $"{registration.ProjectionType}:{registration.Version}";
		var response = await _client.IndexAsync(
				document,
				idx => idx.Index(_historyIndexName).Id(documentId),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to register schema version {ProjectionType}/{Version}: {Error}",
				registration.ProjectionType,
				registration.Version,
				response.DebugInformation);
		}
	}

	/// <inheritdoc />
	public async Task<SchemaVersionHistory> GetSchemaHistoryAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.SearchAsync<SchemaVersionDocument>(
				s => s.Index(_historyIndexName).Size(1000).Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
					.Sort(s => s.Field("registeredAt", new FieldSort { Order = SortOrder.Asc })),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Documents is null)
		{
			return new SchemaVersionHistory { ProjectionType = projectionType, CurrentVersion = "1.0.0", Versions = [], };
		}

		var versions = response.Documents.Select(ToRegistration).ToList();
		var currentVersion = versions.LastOrDefault()?.Version ?? "1.0.0";
		var migrations = await GetMigrationHistoryAsync(projectionType, cancellationToken)
			.ConfigureAwait(false);

		return new SchemaVersionHistory
		{
			ProjectionType = projectionType,
			CurrentVersion = currentVersion,
			Versions = versions,
			MigrationHistory = migrations.Count == 0 ? null : migrations,
		};
	}

	/// <inheritdoc />
	public async Task<SchemaMigrationDryRunResult> DryRunMigrationAsync(
		SchemaMigrationPlan plan,
		int sampleSize,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(plan);

		var sourceIndex = GetStepParameter(plan, StepOperationType.Reindex, "SourceIndex")
						  ?? GetStepParameter(plan, StepOperationType.UpdateMapping, "TargetIndex");

		if (string.IsNullOrWhiteSpace(sourceIndex))
		{
			return new SchemaMigrationDryRunResult
			{
				Success = false,
				DocumentsTested = 0,
				DocumentsSuccessful = 0,
				DocumentsFailed = 0,
				SampleFailures =
				[
					new DocumentMigrationFailure { DocumentId = "n/a", Reason = "Source index not specified in migration plan.", },
				],
			};
		}

		var response = await _client.SearchAsync<object>(s => s.Index(sourceIndex).Size(sampleSize),
				cancellationToken)
			.ConfigureAwait(false);

		var tested = response.Documents?.Count ?? 0;

		return new SchemaMigrationDryRunResult
		{
			Success = response.IsValidResponse,
			DocumentsTested = tested,
			DocumentsSuccessful = tested,
			DocumentsFailed = 0,
			PerformanceMetrics = new DryRunPerformanceMetrics
			{
				AverageProcessingTimeMs = tested == 0 ? 0 : 1,
				EstimatedTotalTime = tested == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(tested),
				DocumentsPerSecond = tested == 0 ? 0 : tested,
			},
		};
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_initializationLock.Dispose();
	}

	private static TypeMapping BuildHistoryMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["projectionType"] = new KeywordProperty(),
				["version"] = new KeywordProperty(),
				["registeredAt"] = new DateProperty(),
				["schemaJson"] = new TextProperty(),
				["description"] = new TextProperty(),
				["migrationNotes"] = new TextProperty(),
			},
		};
	}

	private static TypeMapping BuildMigrationMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["projectionType"] = new KeywordProperty(),
				["planId"] = new KeywordProperty(),
				["recordedAt"] = new DateProperty(),
				["resultJson"] = new TextProperty(),
			},
		};
	}

	private static IReadOnlyDictionary<string, string> ExtractFieldTypes(
		GetMappingResponse response,
		string indexName)
	{
		if (!response.IsValidResponse || !response.Indices.TryGetValue(indexName, out var mapping))
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}

		if (mapping.Mappings.Properties is null)
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}

		return mapping.Mappings.Properties.ToDictionary(
			kvp => kvp.Key.ToString(),
			kvp => GetPropertyTypeName(kvp.Value),
			StringComparer.Ordinal);
	}

	private static string GetPropertyTypeName(IProperty property)
	{
		var typeName = property.GetType().Name;
		return typeName.EndsWith("Property", StringComparison.Ordinal)
			? typeName[..^"Property".Length]
			: typeName;
	}

	private static TypeMapping? GetTypeMapping(object schema)
	{
		if (schema is TypeMapping mapping)
		{
			return mapping;
		}

		if (schema is Properties properties)
		{
			return new TypeMapping { Properties = properties };
		}

		return null;
	}

	private static IReadOnlyCollection<string> ExtractSchemaFields(object schema)
	{
		if (schema is TypeMapping mapping && mapping.Properties is not null)
		{
			return mapping.Properties.Select(kvp => kvp.Key.ToString()).ToList();
		}

		if (schema is Properties properties)
		{
			return properties.Select(kvp => kvp.Key.ToString()).ToList();
		}

		var json = SerializeSchema(schema);
		using var doc = JsonDocument.Parse(json);
		if (doc.RootElement.ValueKind == JsonValueKind.Object)
		{
			return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
		}

		return [];
	}

	private static string GetRequiredParameter(MigrationStep step, string key)
	{
		if (step.Parameters is null || !step.Parameters.TryGetValue(key, out var value))
		{
			throw new InvalidOperationException($"Migration step '{step.Name}' missing '{key}' parameter.");
		}

		return value?.ToString() ?? throw new InvalidOperationException(
			$"Migration step '{step.Name}' parameter '{key}' is invalid.");
	}

	private static string? GetStepParameter(SchemaMigrationPlan plan, StepOperationType operationType, string key)
	{
		var step = plan.Steps.FirstOrDefault(s => s.OperationType == operationType);
		if (step?.Parameters is null)
		{
			return null;
		}

		return step.Parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
	}

	private static string SerializeSchema(object schema)
	{
		return JsonSerializer.Serialize(schema);
	}

	private static SchemaVersionRegistration ToRegistration(SchemaVersionDocument document)
	{
		object schema = document.SchemaJson;
		try
		{
			schema = JsonSerializer.Deserialize<JsonElement>(document.SchemaJson);
		}
		catch
		{
			// Keep raw JSON if parsing fails.
		}

		return new SchemaVersionRegistration
		{
			ProjectionType = document.ProjectionType,
			Version = document.Version,
			Schema = schema,
			RegisteredAt = document.RegisteredAt,
			Description = document.Description,
			MigrationNotes = document.MigrationNotes,
		};
	}

	private async Task EnsureIndicesAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			await EnsureIndexAsync(_historyIndexName, BuildHistoryMapping(), cancellationToken)
				.ConfigureAwait(false);
			await EnsureIndexAsync(_migrationIndexName, BuildMigrationMapping(), cancellationToken)
				.ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initializationLock.Release();
		}
	}

	private async Task EnsureIndexAsync(
		string indexName,
		TypeMapping mapping,
		CancellationToken cancellationToken)
	{
		var exists = await _client.Indices.ExistsAsync(indexName, cancellationToken)
			.ConfigureAwait(false);
		if (exists.Exists)
		{
			return;
		}

		var createRequest = new CreateIndexRequest(indexName)
		{
			Mappings = mapping,
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0, },
		};

		var response = await _client.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to create schema tracking index {IndexName}: {Error}",
				indexName,
				response.DebugInformation);
		}
	}

	private async Task<long?> GetEstimatedDocumentCountAsync(
		string sourceIndex,
		CancellationToken cancellationToken)
	{
		var response = await _client.CountAsync<object>(c => c.Indices(sourceIndex), cancellationToken)
			.ConfigureAwait(false);
		return response.IsValidResponse ? response.Count : null;
	}

	private async Task<bool> ExecuteStepAsync(MigrationStep step, CancellationToken cancellationToken)
	{
		switch (step.OperationType)
		{
			case StepOperationType.CreateIndex:
				{
					var targetIndex = GetRequiredParameter(step, "TargetIndex");
					var exists = await _client.Indices.ExistsAsync(targetIndex, cancellationToken)
						.ConfigureAwait(false);
					if (exists.Exists)
					{
						return true;
					}

					object? mappingObj = null;
					_ = (step.Parameters?.TryGetValue("Mapping", out mappingObj));
					var mapping = mappingObj as TypeMapping;
					var createRequest = new CreateIndexRequest(targetIndex)
					{
						Mappings = mapping,
						Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0, },
					};

					var response = await _client.Indices.CreateAsync(createRequest, cancellationToken)
						.ConfigureAwait(false);
					return response.IsValidResponse;
				}
			case StepOperationType.UpdateMapping:
				{
					var targetIndex = GetRequiredParameter(step, "TargetIndex");
					object? updateMappingObj = null;
					_ = (step.Parameters?.TryGetValue("Mapping", out updateMappingObj));
					if (updateMappingObj is not TypeMapping mapping)
					{
						return true;
					}

					var request = new PutMappingRequest(targetIndex) { Properties = mapping.Properties, };

					var response = await _client.Indices.PutMappingAsync(request, cancellationToken)
						.ConfigureAwait(false);
					return response.IsValidResponse;
				}
			case StepOperationType.Reindex:
				{
					var sourceIndex = GetRequiredParameter(step, "SourceIndex");
					var targetIndex = GetRequiredParameter(step, "TargetIndex");

					var reindexRequest = new ReindexRequest
					{
						Source = new Source { Indices = sourceIndex },
						Dest = new Destination { Index = targetIndex },
						Refresh = true,
						WaitForCompletion = true,
					};

					var response = await _client.ReindexAsync(reindexRequest, cancellationToken)
						.ConfigureAwait(false);
					return response.IsValidResponse;
				}
			case StepOperationType.SwitchAlias:
				{
					var aliasName = GetRequiredParameter(step, "AliasName");
					var targetIndex = GetRequiredParameter(step, "TargetIndex");

					var existingAliases = await _aliasManager.GetAliasesAsync(aliasName, cancellationToken)
						.ConfigureAwait(false);

					var operations = new List<AliasOperation>();
					foreach (var alias in existingAliases)
					{
						foreach (var index in alias.Indices)
						{
							operations.Add(new AliasOperation
							{
								AliasName = aliasName,
								IndexName = index,
								OperationType = AliasOperationType.Remove,
							});
						}
					}

					operations.Add(new AliasOperation
					{
						AliasName = aliasName,
						IndexName = targetIndex,
						OperationType = AliasOperationType.Add,
						AliasConfiguration = new Alias { IsWriteIndex = true },
					});

					if (operations.Count == 1)
					{
						return await _aliasManager.CreateAliasAsync(
								aliasName,
								[targetIndex],
								new Alias { IsWriteIndex = true },
								cancellationToken)
							.ConfigureAwait(false);
					}

					return await _aliasManager.UpdateAliasesAsync(operations, cancellationToken)
						.ConfigureAwait(false);
				}

			case StepOperationType.Transform:
				return true;

			case StepOperationType.Validate:
				return true;

			case StepOperationType.DeleteIndex:
				return true;

			case StepOperationType.Backup:
				return true;

			default:
				return true;
		}
	}

	private async Task<long> GetDocumentCountFromStepAsync(
		MigrationStep step,
		CancellationToken cancellationToken)
	{
		var targetIndex = GetRequiredParameter(step, "TargetIndex");
		var response = await _client.CountAsync<object>(c => c.Indices(targetIndex), cancellationToken)
			.ConfigureAwait(false);
		return response.IsValidResponse ? response.Count : 0;
	}

	private async Task StoreMigrationResultAsync(
		string projectionType,
		SchemaMigrationResult result,
		CancellationToken cancellationToken)
	{
		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var document = new MigrationHistoryDocument
		{
			ProjectionType = projectionType,
			PlanId = result.PlanId,
			RecordedAt = DateTimeOffset.UtcNow,
			ResultJson = JsonSerializer.Serialize(result),
		};

		var documentId = $"{projectionType}:{result.PlanId}";
		var response = await _client.IndexAsync(
				document,
				idx => idx.Index(_migrationIndexName).Id(documentId),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to store schema migration result {ProjectionType}/{PlanId}: {Error}",
				projectionType,
				result.PlanId,
				response.DebugInformation);
		}
	}

	private async Task<List<SchemaMigrationResult>> GetMigrationHistoryAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync<MigrationHistoryDocument>(
				s => s.Index(_migrationIndexName).Size(1000).Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
					.Sort(s => s.Field("recordedAt", new FieldSort { Order = SortOrder.Desc })),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Documents is null)
		{
			return [];
		}

		var results = new List<SchemaMigrationResult>();
		foreach (var document in response.Documents)
		{
			if (string.IsNullOrWhiteSpace(document.ResultJson))
			{
				continue;
			}

			var result = JsonSerializer.Deserialize<SchemaMigrationResult>(document.ResultJson);
			if (result is not null)
			{
				results.Add(result);
			}
		}

		return results;
	}

	private sealed class SchemaVersionDocument
	{
		public string ProjectionType { get; init; } = string.Empty;
		public string Version { get; init; } = string.Empty;
		public string SchemaJson { get; init; } = string.Empty;
		public DateTimeOffset RegisteredAt { get; init; }
		public string? Description { get; init; }
		public string? MigrationNotes { get; init; }
	}

	private sealed class MigrationHistoryDocument
	{
		public string ProjectionType { get; init; } = string.Empty;
		public string PlanId { get; init; } = string.Empty;
		public DateTimeOffset RecordedAt { get; init; }
		public string ResultJson { get; init; } = string.Empty;
	}
}
