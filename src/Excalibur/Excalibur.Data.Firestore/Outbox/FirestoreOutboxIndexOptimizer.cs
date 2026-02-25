// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Outbox;

/// <summary>
/// Validates and documents required composite indexes for Firestore outbox queries.
/// </summary>
/// <remarks>
/// <para>
/// Firestore requires composite indexes for queries that combine equality filters
/// with ordering or inequality filters on different fields. The outbox store relies
/// on several such queries (e.g., <c>status == Staged ORDER BY createdAt</c>).
/// </para>
/// <para>
/// This class provides runtime validation by executing representative queries
/// against the Firestore collection. If a query fails with a
/// <c>FailedPrecondition</c> status, the required composite index is missing
/// and the optimizer logs the necessary index definition.
/// </para>
/// <para>
/// Reference: <see href="https://firebase.google.com/docs/firestore/query-data/indexing">
/// Firestore Indexing Documentation</see>.
/// </para>
/// </remarks>
public sealed partial class FirestoreOutboxIndexOptimizer
{
	private readonly FirestoreIndexOptions _indexOptions;
	private readonly FirestoreOutboxOptions _outboxOptions;
	private readonly ILogger<FirestoreOutboxIndexOptimizer> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreOutboxIndexOptimizer"/> class.
	/// </summary>
	/// <param name="indexOptions"> The index configuration options. </param>
	/// <param name="outboxOptions"> The Firestore outbox store options. </param>
	/// <param name="logger"> The logger instance. </param>
	public FirestoreOutboxIndexOptimizer(
		IOptions<FirestoreIndexOptions> indexOptions,
		IOptions<FirestoreOutboxOptions> outboxOptions,
		ILogger<FirestoreOutboxIndexOptimizer> logger)
	{
		ArgumentNullException.ThrowIfNull(indexOptions);
		ArgumentNullException.ThrowIfNull(outboxOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_indexOptions = indexOptions.Value;
		_outboxOptions = outboxOptions.Value;
		_logger = logger;
	}

	/// <summary>
	/// Validates that all required composite indexes exist for the outbox collection.
	/// </summary>
	/// <param name="db"> The Firestore database instance. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>
	/// A <see cref="FirestoreIndexValidationResult"/> indicating which indexes are present or missing.
	/// </returns>
	public async Task<FirestoreIndexValidationResult> ValidateIndexesAsync(
		FirestoreDb db,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(db);

		if (!_indexOptions.EnableCompositeIndexValidation)
		{
			LogIndexValidationSkipped(_indexOptions.CollectionName);
			return new FirestoreIndexValidationResult([], []);
		}

		LogIndexValidationStarting(_indexOptions.CollectionName);

		var collection = db.Collection(_indexOptions.CollectionName);
		var presentIndexes = new List<CompositeIndexDefinition>();
		var missingIndexes = new List<CompositeIndexDefinition>();

		var allIndexes = _indexOptions.RequiredIndexes
			.Concat(_indexOptions.AdditionalIndexes)
			.ToList();

		foreach (var index in allIndexes)
		{
			var isPresent = await ValidateSingleIndexAsync(collection, index, cancellationToken).ConfigureAwait(false);

			if (isPresent)
			{
				presentIndexes.Add(index);
			}
			else
			{
				missingIndexes.Add(index);
				LogCompositeIndexMissing(
					index.Name,
					string.Join(", ", index.Fields),
					index.Description);
			}
		}

		if (missingIndexes.Count == 0)
		{
			LogAllIndexesPresent(_indexOptions.CollectionName, presentIndexes.Count);
		}
		else
		{
			LogMissingIndexesSummary(_indexOptions.CollectionName, missingIndexes.Count, allIndexes.Count);
		}

		return new FirestoreIndexValidationResult(presentIndexes, missingIndexes);
	}

	/// <summary>
	/// Gets the Firestore CLI commands to create all required composite indexes.
	/// </summary>
	/// <returns> A list of CLI command strings for creating the required indexes. </returns>
	public IReadOnlyList<string> GetIndexCreationCommands()
	{
		var commands = new List<string>();
		var collectionName = _indexOptions.CollectionName;

		var allIndexes = _indexOptions.RequiredIndexes
			.Concat(_indexOptions.AdditionalIndexes)
			.ToList();

		foreach (var index in allIndexes)
		{
			var fieldArgs = string.Join(",", index.Fields.Select(f => $"{f},ASCENDING"));
			var command = $"gcloud firestore indexes composite create --collection-group={collectionName} --field-config={fieldArgs}";
			commands.Add(command);
		}

		return commands;
	}

	private static async Task<bool> ValidateSingleIndexAsync(
		CollectionReference collection,
		CompositeIndexDefinition index,
		CancellationToken cancellationToken)
	{
		try
		{
			// Build a representative query using the index fields
			// First field is always an equality filter, subsequent fields use ordering
			var query = collection.Limit(1);

			if (index.Fields.Count >= 1)
			{
				query = query.WhereEqualTo(index.Fields[0], 0);
			}

			if (index.Fields.Count >= 2)
			{
				query = query.OrderBy(index.Fields[1]);
			}

			// Execute the query - if the composite index is missing, Firestore throws FailedPrecondition
			_ = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
		{
			// FailedPrecondition indicates the composite index is missing
			return false;
		}
	}

	[LoggerMessage(DataFirestoreEventId.CompositeIndexRequired, LogLevel.Information,
		"Starting composite index validation for collection '{CollectionName}'")]
	private partial void LogIndexValidationStarting(string collectionName);

	[LoggerMessage(DataFirestoreEventId.CompositeIndexRequired + 1, LogLevel.Information,
		"Composite index validation skipped for collection '{CollectionName}' (disabled)")]
	private partial void LogIndexValidationSkipped(string collectionName);

	[LoggerMessage(DataFirestoreEventId.CompositeIndexRequired + 2, LogLevel.Warning,
		"Missing composite index '{IndexName}' on fields ({Fields}) — required for {Description}")]
	private partial void LogCompositeIndexMissing(string indexName, string fields, string description);

	[LoggerMessage(DataFirestoreEventId.CompositeIndexRequired + 3, LogLevel.Information,
		"All {Count} composite indexes present for collection '{CollectionName}'")]
	private partial void LogAllIndexesPresent(string collectionName, int count);

	[LoggerMessage(DataFirestoreEventId.CompositeIndexRequired + 4, LogLevel.Warning,
		"Collection '{CollectionName}' is missing {MissingCount} of {TotalCount} required composite indexes")]
	private partial void LogMissingIndexesSummary(string collectionName, int missingCount, int totalCount);
}

/// <summary>
/// Result of composite index validation for a Firestore outbox collection.
/// </summary>
/// <param name="PresentIndexes"> Indexes that were validated as present. </param>
/// <param name="MissingIndexes"> Indexes that are required but missing. </param>
public sealed record FirestoreIndexValidationResult(
	IReadOnlyList<CompositeIndexDefinition> PresentIndexes,
	IReadOnlyList<CompositeIndexDefinition> MissingIndexes)
{
	/// <summary>
	/// Gets a value indicating whether all required indexes are present.
	/// </summary>
	/// <value><see langword="true"/> if no indexes are missing; otherwise, <see langword="false"/>.</value>
	public bool AllIndexesPresent => MissingIndexes.Count == 0;
}
