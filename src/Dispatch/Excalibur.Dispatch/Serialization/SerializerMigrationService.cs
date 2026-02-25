// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Service for migrating data between serialization formats in persistence stores.
/// </summary>
/// <remarks>
/// <para>
/// This service implements Phase 3 of the four-phase migration strategy.
/// It provides methods for bulk migration of existing data from one serializer format to another.
/// </para>
/// <para>
/// <b>Key Features:</b>
/// </para>
/// <list type="bullet">
///   <item>Batch processing to prevent memory pressure</item>
///   <item>Progress reporting via <see cref="IProgress{T}"/></item>
///   <item>Idempotent execution (safe to re-run)</item>
///   <item>Optional read-back verification</item>
///   <item>Configurable error handling</item>
/// </list>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// // Migrate outbox from MemoryPack to System.Text.Json
/// var progress = new Progress&lt;EncryptionMigrationProgress&gt;(p =>
///     Console.WriteLine($"Progress: {p}"));
///
/// var result = await migrationService.MigrateStoreAsync(
///     outboxMigrationStore,
///     SerializerIds.MemoryPack,
///     SerializerIds.SystemTextJson,
///     progress);
/// </code>
/// <para>
/// See the serialization migration strategy documentation.
/// </para>
/// </remarks>
public partial class SerializerMigrationService
{
	private readonly ISerializerRegistry _registry;
	private readonly ILogger<SerializerMigrationService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializerMigrationService"/> class.
	/// </summary>
	/// <param name="registry">The serializer registry containing registered serializers.</param>
	/// <param name="logger">The logger instance.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when registry or logger is null.
	/// </exception>
	public SerializerMigrationService(
		ISerializerRegistry registry,
		ILogger<SerializerMigrationService> logger)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Result of migrating a single record.
	/// </summary>
	private enum EncryptionMigrationResult
	{
		/// <summary>Record was successfully migrated.</summary>
		Migrated,

		/// <summary>Record was skipped (already in target format or invalid).</summary>
		Skipped,

		/// <summary>Record migration failed.</summary>
		Failed
	}

	/// <summary>
	/// Migrates all records in a store from one serializer format to another.
	/// </summary>
	/// <param name="store">The migration store to process.</param>
	/// <param name="sourceSerializerId">The source serializer ID (magic byte) to migrate from.</param>
	/// <param name="targetSerializerId">The target serializer ID (magic byte) to migrate to.</param>
	/// <param name="progress">Optional progress reporter.</param>
	/// <param name="options">Optional migration options. Uses defaults if not specified.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The final migration progress with total counts.</returns>
	/// <exception cref="ArgumentNullException">Thrown when store is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when source or target serializer is not registered.
	/// </exception>
	/// <exception cref="OperationCanceledException">
	/// Thrown when cancellation is requested.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The migration is <b>idempotent</b> - running it multiple times is safe.
	/// Records already in the target format are automatically skipped.
	/// </para>
	/// <para>
	/// The operation processes records in batches to limit memory usage.
	/// Progress is reported after each batch is processed.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode(
		"Serializer migration uses runtime type resolution and serialization which may require preserved members.")]
	[RequiresDynamicCode(
		"Serializer migration uses runtime type resolution and serialization which may require dynamic code generation.")]
	public async Task<EncryptionMigrationProgress> MigrateStoreAsync(
		IMigrationStore store,
		byte sourceSerializerId,
		byte targetSerializerId,
		CancellationToken cancellationToken,
		IProgress<EncryptionMigrationProgress>? progress = null,
		MigrationOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(store);

		options ??= new MigrationOptions();

		// Validate serializers are registered
		var sourceSerializer = _registry.GetById(sourceSerializerId)
							   ?? throw new InvalidOperationException(
								   string.Format(
									   CultureInfo.CurrentCulture,
									   Resources.SerializerMigrationService_SourceSerializerNotRegisteredFormat,
									   sourceSerializerId));

		var targetSerializer = _registry.GetById(targetSerializerId)
							   ?? throw new InvalidOperationException(
								   string.Format(
									   CultureInfo.CurrentCulture,
									   Resources.SerializerMigrationService_TargetSerializerNotRegisteredFormat,
									   targetSerializerId));

		LogMigrationStarting(
			store.StoreName,
			sourceSerializer.Name,
			sourceSerializerId,
			targetSerializer.Name,
			targetSerializerId,
			options.BatchSize);

		// Get initial count for progress estimation
		int? estimatedRemaining = null;
		try
		{
			estimatedRemaining = await store.CountPendingMigrationsAsync(
				sourceSerializerId, cancellationToken).ConfigureAwait(false);

			LogMigrationEstimatedRecords(estimatedRemaining.Value, store.StoreName);
		}
		catch (OperationCanceledException)
		{
			throw; // Re-throw cancellation - don't swallow it
		}
		catch (Exception ex)
		{
			LogMigrationCountFailed(ex, store.StoreName);
		}

		var totalMigrated = 0;
		var totalFailed = 0;
		var totalSkipped = 0;
		var consecutiveFailures = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			// Get batch of records to migrate
			var batch = await store.GetBatchForMigrationAsync(
				sourceSerializerId,
				targetSerializerId,
				options.BatchSize,
				cancellationToken).ConfigureAwait(false);

			if (batch.Count == 0)
			{
				LogMigrationNoMoreRecords(store.StoreName);
				break;
			}

			LogMigrationBatchProcessing(batch.Count, store.StoreName);

			foreach (var record in batch)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					var result = await MigrateRecordAsync(
						store,
						record,
						sourceSerializer,
						sourceSerializerId,
						targetSerializer,
						targetSerializerId,
						options.EnableReadBackVerification,
						cancellationToken).ConfigureAwait(false);

					switch (result)
					{
						case EncryptionMigrationResult.Migrated:
							totalMigrated++;
							consecutiveFailures = 0;
							break;

						case EncryptionMigrationResult.Skipped:
							totalSkipped++;
							consecutiveFailures = 0;
							break;

						case EncryptionMigrationResult.Failed:
							totalFailed++;
							consecutiveFailures++;
							break;

						default:
							totalFailed++;
							consecutiveFailures++;
							break;
					}

					// Check consecutive failure limit
					if (options.MaxConsecutiveFailures > 0 &&
						consecutiveFailures >= options.MaxConsecutiveFailures)
					{
						LogMigrationAborted(consecutiveFailures, store.StoreName);

						throw new InvalidOperationException(
							string.Format(
								CultureInfo.CurrentCulture,
								Resources.SerializerMigrationService_MigrationAbortedAfterFailuresFormat,
								consecutiveFailures));
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException
											   and not InvalidOperationException)
				{
					totalFailed++;
					consecutiveFailures++;

					LogMigrationRecordFailed(ex, record.Id, store.StoreName);

					if (!options.ContinueOnFailure)
					{
						throw;
					}
				}
			}

			// Update estimated remaining
			if (estimatedRemaining.HasValue)
			{
				estimatedRemaining = Math.Max(0,
					estimatedRemaining.Value - batch.Count);
			}

			// Report progress
			var currentProgress = new EncryptionMigrationProgress(
				totalMigrated,
				totalFailed,
				totalSkipped,
				batch.Count,
				estimatedRemaining);

			progress?.Report(currentProgress);

			LogMigrationBatchComplete(store.StoreName, currentProgress);

			// Apply delay between batches if configured
			if (options.DelayBetweenBatchesMs > 0)
			{
				await Task.Delay(options.DelayBetweenBatchesMs, cancellationToken).ConfigureAwait(false);
			}
		}

		var finalProgress = new EncryptionMigrationProgress(
			totalMigrated,
			totalFailed,
			totalSkipped,
			0,
			0);

		LogMigrationComplete(store.StoreName, finalProgress);

		return finalProgress;
	}

	/// <summary>
	/// Migrates a single record from source to target serializer format.
	/// </summary>
	[RequiresUnreferencedCode(
		"Serializer migration uses runtime type resolution and serialization which may require preserved members.")]
	[RequiresDynamicCode(
		"Serializer migration uses runtime type resolution and serialization which may require dynamic code generation.")]
	private async Task<EncryptionMigrationResult> MigrateRecordAsync(
		IMigrationStore store,
		IMigrationRecord record,
		IPluggableSerializer sourceSerializer,
		byte sourceSerializerId,
		IPluggableSerializer targetSerializer,
		byte targetSerializerId,
		bool enableReadBackVerification,
		CancellationToken cancellationToken)
	{
		// Validate payload has data
		if (record.Payload == null || record.Payload.Length < 2)
		{
			LogRecordInvalidPayload(record.Id);
			return EncryptionMigrationResult.Skipped;
		}

		// Check magic byte - skip if already in target format (idempotent)
		var currentSerializerId = record.Payload[0];
		if (currentSerializerId == targetSerializerId)
		{
			LogRecordAlreadyTargetFormat(record.Id);
			return EncryptionMigrationResult.Skipped;
		}

		// Verify it's actually the source format
		if (currentSerializerId != sourceSerializerId)
		{
			LogRecordUnexpectedSerializer(record.Id, currentSerializerId, sourceSerializerId);
			return EncryptionMigrationResult.Skipped;
		}

		// Extract payload (skip magic byte)
		var sourcePayload = record.Payload.AsSpan(1);

		// Resolve the type from TypeName - required for binary serializers
		if (string.IsNullOrEmpty(record.TypeName))
		{
			LogRecordMissingTypeName(record.Id);
			return EncryptionMigrationResult.Skipped;
		}

		var recordType = TypeResolution.TypeResolver.ResolveType(record.TypeName);
		if (recordType == null)
		{
			LogRecordUnresolvableTypeName(record.Id, record.TypeName);
			return EncryptionMigrationResult.Skipped;
		}

		// Deserialize with source serializer using resolved type
		object obj;
		try
		{
			obj = sourceSerializer.DeserializeObject(sourcePayload, recordType);
		}
		catch (Exception ex)
		{
			LogRecordDeserializeFailed(ex, record.Id, sourceSerializer.Name);
			return EncryptionMigrationResult.Failed;
		}

		// Re-serialize with target serializer
		byte[] newPayloadContent;
		try
		{
			newPayloadContent = targetSerializer.SerializeObject(obj, obj.GetType());
		}
		catch (Exception ex)
		{
			LogRecordSerializeFailed(ex, record.Id, targetSerializer.Name);
			return EncryptionMigrationResult.Failed;
		}

		// Build new payload with magic byte
		var newPayload = new byte[newPayloadContent.Length + 1];
		newPayload[0] = targetSerializerId;
		Buffer.BlockCopy(newPayloadContent, 0, newPayload, 1, newPayloadContent.Length);

		// Update in store
		var updated = await store.UpdatePayloadAsync(record.Id, newPayload, cancellationToken).ConfigureAwait(false);
		if (!updated)
		{
			LogRecordNotFoundDuringUpdate(record.Id);
			return EncryptionMigrationResult.Skipped;
		}

		// Read-back verification if enabled
		if (enableReadBackVerification)
		{
			var verificationPayload = await store.GetPayloadAsync(record.Id, cancellationToken).ConfigureAwait(false);
			if (verificationPayload == null)
			{
				LogRecordNotFoundDuringVerification(record.Id);
				return EncryptionMigrationResult.Migrated;
			}

			if (verificationPayload[0] != targetSerializerId)
			{
				LogRecordVerificationMagicByteMismatch(
					record.Id,
					verificationPayload[0],
					targetSerializerId);
				return EncryptionMigrationResult.Failed;
			}

			// Verify we can deserialize the new payload using the resolved type
			try
			{
				var verificationPayloadContent = verificationPayload.AsSpan(1);
				_ = targetSerializer.DeserializeObject(verificationPayloadContent, recordType);
			}
			catch (Exception ex)
			{
				LogRecordVerificationDeserializeFailed(ex, record.Id);
				return EncryptionMigrationResult.Failed;
			}
		}

		LogRecordMigrated(record.Id, sourceSerializer.Name, targetSerializer.Name);

		return EncryptionMigrationResult.Migrated;
	}

	#region LoggerMessage Definitions

	[LoggerMessage(LogLevel.Information,
		"Starting migration of {StoreName} from '{SourceSerializer}' (0x{SourceId:X2}) " +
		"to '{TargetSerializer}' (0x{TargetId:X2}). Batch size: {BatchSize}")]
	private partial void LogMigrationStarting(
		string storeName,
		string sourceSerializer,
		byte sourceId,
		string targetSerializer,
		byte targetId,
		int batchSize);

	[LoggerMessage(LogLevel.Information,
		"Estimated {Count} records to migrate in {StoreName}")]
	private partial void LogMigrationEstimatedRecords(int count, string storeName);

	[LoggerMessage(LogLevel.Warning,
		"Failed to get pending migration count for {StoreName}. Progress estimation will be unavailable.")]
	private partial void LogMigrationCountFailed(Exception exception, string storeName);

	[LoggerMessage(LogLevel.Information,
		"No more records to migrate in {StoreName}")]
	private partial void LogMigrationNoMoreRecords(string storeName);

	[LoggerMessage(LogLevel.Debug,
		"Processing batch of {Count} records in {StoreName}")]
	private partial void LogMigrationBatchProcessing(int count, string storeName);

	[LoggerMessage(LogLevel.Error,
		"Migration aborted: {ConsecutiveFailures} consecutive failures in {StoreName}")]
	private partial void LogMigrationAborted(int consecutiveFailures, string storeName);

	[LoggerMessage(LogLevel.Error,
		"Failed to migrate record {RecordId} in {StoreName}")]
	private partial void LogMigrationRecordFailed(Exception exception, string recordId, string storeName);

	[LoggerMessage(LogLevel.Debug,
		"Batch complete in {StoreName}: {Progress}")]
	private partial void LogMigrationBatchComplete(string storeName, EncryptionMigrationProgress progress);

	[LoggerMessage(LogLevel.Information,
		"Migration complete for {StoreName}: {Progress}")]
	private partial void LogMigrationComplete(string storeName, EncryptionMigrationProgress progress);

	[LoggerMessage(LogLevel.Warning,
		"Record {RecordId} has invalid payload (null or too short). Skipping.")]
	private partial void LogRecordInvalidPayload(string recordId);

	[LoggerMessage(LogLevel.Debug,
		"Record {RecordId} is already in target format. Skipping.")]
	private partial void LogRecordAlreadyTargetFormat(string recordId);

	[LoggerMessage(LogLevel.Warning,
		"Record {RecordId} has unexpected serializer ID 0x{ActualId:X2} (expected source: 0x{ExpectedId:X2}). Skipping.")]
	private partial void LogRecordUnexpectedSerializer(string recordId, byte actualId, byte expectedId);

	[LoggerMessage(LogLevel.Warning,
		"Record {RecordId} has no TypeName. Binary serializers require type info. Skipping.")]
	private partial void LogRecordMissingTypeName(string recordId);

	[LoggerMessage(LogLevel.Warning,
		"Record {RecordId} has unresolvable TypeName '{TypeName}'. Skipping.")]
	private partial void LogRecordUnresolvableTypeName(string recordId, string? typeName);

	[LoggerMessage(LogLevel.Error,
		"Failed to deserialize record {RecordId} with source serializer '{Serializer}'")]
	private partial void LogRecordDeserializeFailed(Exception exception, string recordId, string serializer);

	[LoggerMessage(LogLevel.Error,
		"Failed to serialize record {RecordId} with target serializer '{Serializer}'")]
	private partial void LogRecordSerializeFailed(Exception exception, string recordId, string serializer);

	[LoggerMessage(LogLevel.Warning,
		"Record {RecordId} was not found during update (may have been deleted). Skipping.")]
	private partial void LogRecordNotFoundDuringUpdate(string recordId);

	[LoggerMessage(LogLevel.Warning,
		"Record {RecordId} not found during verification (may have been deleted). Counting as migrated.")]
	private partial void LogRecordNotFoundDuringVerification(string recordId);

	[LoggerMessage(LogLevel.Error,
		"Verification failed for record {RecordId}: magic byte is 0x{ActualId:X2}, expected 0x{ExpectedId:X2}")]
	private partial void LogRecordVerificationMagicByteMismatch(string recordId, byte actualId, byte expectedId);

	[LoggerMessage(LogLevel.Error,
		"Verification failed for record {RecordId}: deserialization error")]
	private partial void LogRecordVerificationDeserializeFailed(Exception exception, string recordId);

	[LoggerMessage(LogLevel.Debug,
		"Successfully migrated record {RecordId} from '{Source}' to '{Target}'")]
	private partial void LogRecordMigrated(string recordId, string source, string target);

	#endregion LoggerMessage Definitions
}
