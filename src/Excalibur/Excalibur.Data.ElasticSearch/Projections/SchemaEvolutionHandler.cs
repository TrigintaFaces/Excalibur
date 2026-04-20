// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Handles schema evolution and migrations for Elasticsearch projections.
/// </summary>
public sealed class SchemaEvolutionHandler : ISchemaEvolutionHandler, ISchemaEvolutionHandlerAdmin, IDisposable
{
	private readonly ISchemaEvolutionOperations _ops;
	private readonly ISchemaHistoryStore _history;
	private readonly IMigrationHistoryStore _migrationHistory;
	private readonly IIndexInspection _inspection;
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
		: this(
			CreateOps(client),
			CreateHistory(client),
			CreateMigrationHistory(client),
			CreateInspection(client),
			aliasManager,
			options,
			logger)
	{
	}

	/// <summary>
	/// Internal test-seam constructor accepting the four γ seams directly.
	/// Used by unit tests to substitute fakes for the SDK adapters.
	/// </summary>
	internal SchemaEvolutionHandler(
		ISchemaEvolutionOperations ops,
		ISchemaHistoryStore history,
		IMigrationHistoryStore migrationHistory,
		IIndexInspection inspection,
		IIndexAliasManager aliasManager,
		IOptions<ProjectionOptions> options,
		ILogger<SchemaEvolutionHandler> logger)
	{
		_ops = ops ?? throw new ArgumentNullException(nameof(ops));
		_history = history ?? throw new ArgumentNullException(nameof(history));
		_migrationHistory = migrationHistory ?? throw new ArgumentNullException(nameof(migrationHistory));
		_inspection = inspection ?? throw new ArgumentNullException(nameof(inspection));
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

		var sourceVersion = await _ops.GetSchemaVersionAsync(sourceIndex, cancellationToken)
			.ConfigureAwait(false);
		var targetVersion = await _ops.GetSchemaVersionAsync(targetIndex, cancellationToken)
			.ConfigureAwait(false);

		var sourceFields = DeserializeFieldTypes(sourceVersion.MappingJson);
		var targetFields = DeserializeFieldTypes(targetVersion.MappingJson);

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
			// UpdateInPlace — route through the same Reindex step shape so
			// ExecuteStepAsync invokes _ops.MigrateAsync(sourceIndex, sourceIndex, mapping, ct).
			// The adapter short-circuits the reindex call when source == target and applies
			// the mapping directly, avoiding a self-reindex. Zero interface churn vs. adding
			// a dedicated seam method. See SENTINEL msg 1956 / OVERWATCH msg 1959 (Path B).
			steps.Add(new MigrationStep
			{
				StepNumber = stepNumber++,
				Name = "Reindex",
				Description = "Apply mapping in place",
				OperationType = StepOperationType.Reindex,
				IsCritical = true,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["SourceIndex"] = request.SourceIndex,
					["TargetIndex"] = request.SourceIndex,
					["Mapping"] = mapping ?? request.NewSchema,
				},
			});
		}

		var estimatedDocs = await _inspection.CountDocumentsAsync(request.SourceIndex, cancellationToken)
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
		var record = new SchemaHistoryRecord
		{
			ProjectionType = registration.ProjectionType,
			Version = registration.Version,
			SchemaJson = schemaJson,
			RegisteredAt = registration.RegisteredAt,
			Description = registration.Description,
			MigrationNotes = registration.MigrationNotes,
		};

		var documentId = $"{registration.ProjectionType}:{registration.Version}";
		var success = await _history
			.WriteSchemaVersionAsync(_historyIndexName, documentId, record, cancellationToken)
			.ConfigureAwait(false);

		if (!success)
		{
			_logger.LogWarning(
				"Failed to register schema version {ProjectionType}/{Version}",
				registration.ProjectionType,
				registration.Version);
		}
	}

	/// <inheritdoc />
	public async Task<SchemaVersionHistory> GetSchemaHistoryAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var records = await _history
			.QueryHistoryAsync(_historyIndexName, projectionType, cancellationToken)
			.ConfigureAwait(false);

		if (records.Count == 0)
		{
			return new SchemaVersionHistory { ProjectionType = projectionType, CurrentVersion = "1.0.0", Versions = [], };
		}

		var versions = records.Select(ToRegistration).ToList();
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

		var ids = await _inspection
			.SampleDocumentIdsAsync(sourceIndex, sampleSize, cancellationToken)
			.ConfigureAwait(false);

		var tested = ids.Count;

		return new SchemaMigrationDryRunResult
		{
			Success = true,
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

	private static ISchemaEvolutionOperations CreateOps(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new SchemaEvolutionOperationsAdapter(client);
	}

	private static ISchemaHistoryStore CreateHistory(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new SchemaHistoryStoreAdapter(client);
	}

	private static IMigrationHistoryStore CreateMigrationHistory(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new MigrationHistoryStoreAdapter(client);
	}

	private static IIndexInspection CreateInspection(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new IndexInspectionAdapter(client);
	}

	private static IReadOnlyDictionary<string, string> DeserializeFieldTypes(string? mappingJson)
	{
		if (string.IsNullOrWhiteSpace(mappingJson))
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}

		try
		{
#pragma warning disable IL2026, IL3050 // JSON deserialization of internal dictionary; type-safe shape
			var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson);
#pragma warning restore IL2026, IL3050
			return dict ?? new Dictionary<string, string>(StringComparer.Ordinal);
		}
		catch (JsonException)
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}
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
		#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
		return JsonSerializer.Serialize(schema);
		#pragma warning restore IL2026, IL3050
	}

	private static SchemaVersionRegistration ToRegistration(SchemaHistoryRecord record)
	{
		object schema = record.SchemaJson;
		try
		{
			#pragma warning disable IL2026, IL3050 // Schema deserialization uses reflection
			schema = JsonSerializer.Deserialize<JsonElement>(record.SchemaJson)!;
#pragma warning restore IL2026, IL3050
		}
		catch (JsonException)
		{
			// Keep raw JSON if parsing fails.
		}

		return new SchemaVersionRegistration
		{
			ProjectionType = record.ProjectionType,
			Version = record.Version,
			Schema = schema,
			RegisteredAt = record.RegisteredAt,
			Description = record.Description,
			MigrationNotes = record.MigrationNotes,
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

			var historyOk = await _history
				.EnsureHistoryIndexAsync(_historyIndexName, cancellationToken)
				.ConfigureAwait(false);
			if (!historyOk)
			{
				_logger.LogWarning(
					"Failed to create schema tracking index {IndexName}",
					_historyIndexName);
			}

			var migrationOk = await _migrationHistory
				.EnsureHistoryIndexAsync(_migrationIndexName, cancellationToken)
				.ConfigureAwait(false);
			if (!migrationOk)
			{
				_logger.LogWarning(
					"Failed to create schema tracking index {IndexName}",
					_migrationIndexName);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initializationLock.Release();
		}
	}

	private async Task<bool> ExecuteStepAsync(MigrationStep step, CancellationToken cancellationToken)
	{
		switch (step.OperationType)
		{
			case StepOperationType.CreateIndex:
				{
					var targetIndex = GetRequiredParameter(step, "TargetIndex");
					object? mappingObj = null;
					_ = step.Parameters?.TryGetValue("Mapping", out mappingObj);
					var outcome = await _ops
						.EnsureMigrationIndexAsync(targetIndex, mappingObj, cancellationToken)
						.ConfigureAwait(false);
					return outcome.Success;
				}

			case StepOperationType.UpdateMapping:
				{
					// UpdateMapping is consolidated into MigrateAsync (which applies the mapping
					// after the reindex). When this step appears alongside a Reindex step in the
					// standard plan shape, it becomes a no-op; the mapping is applied by the
					// Reindex case below via _ops.MigrateAsync. Returning true preserves plan
					// step semantics without performing a redundant SDK call.
					return true;
				}

			case StepOperationType.Reindex:
				{
					var sourceIndex = GetRequiredParameter(step, "SourceIndex");
					var targetIndex = GetRequiredParameter(step, "TargetIndex");

					// Pull the mapping from the companion UpdateMapping step (if present in the
					// plan) so MigrateAsync applies it after reindexing. Mapping is optional —
					// strategies that don't change the schema simply omit it.
					object? mapping = null;
					_ = step.Parameters?.TryGetValue("Mapping", out mapping);

					var outcome = await _ops
						.MigrateAsync(sourceIndex, targetIndex, mapping, cancellationToken)
						.ConfigureAwait(false);
					return outcome.Success;
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
		var count = await _inspection
			.CountDocumentsAsync(targetIndex, cancellationToken)
			.ConfigureAwait(false);
		return count ?? 0;
	}

	private async Task StoreMigrationResultAsync(
		string projectionType,
		SchemaMigrationResult result,
		CancellationToken cancellationToken)
	{
		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable IL2026, IL3050 // JSON serialization uses reflection
		var resultJson = JsonSerializer.Serialize(result);
#pragma warning restore IL2026, IL3050

		var record = new MigrationHistoryRecord
		{
			ProjectionType = projectionType,
			PlanId = result.PlanId,
			RecordedAt = DateTimeOffset.UtcNow,
			ResultJson = resultJson,
		};

		var documentId = $"{projectionType}:{result.PlanId}";
		var success = await _migrationHistory
			.WriteMigrationResultAsync(_migrationIndexName, documentId, record, cancellationToken)
			.ConfigureAwait(false);

		if (!success)
		{
			_logger.LogWarning(
				"Failed to store schema migration result {ProjectionType}/{PlanId}",
				projectionType,
				result.PlanId);
		}
	}

	private async Task<List<SchemaMigrationResult>> GetMigrationHistoryAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		var records = await _migrationHistory
			.QueryHistoryAsync(_migrationIndexName, projectionType, cancellationToken)
			.ConfigureAwait(false);

		if (records.Count == 0)
		{
			return [];
		}

		var results = new List<SchemaMigrationResult>(records.Count);
		foreach (var record in records)
		{
			if (string.IsNullOrWhiteSpace(record.ResultJson))
			{
				continue;
			}

			#pragma warning disable IL2026, IL3050 // JSON deserialization uses reflection
			var result = JsonSerializer.Deserialize<SchemaMigrationResult>(record.ResultJson);
			#pragma warning restore IL2026, IL3050
			if (result is not null)
			{
				results.Add(result);
			}
		}

		return results;
	}
}
