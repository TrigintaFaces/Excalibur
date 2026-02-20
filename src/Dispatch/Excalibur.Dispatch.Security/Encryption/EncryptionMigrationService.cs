// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides services for migrating encrypted data between providers, algorithms, or key versions.
/// </summary>
public sealed partial class EncryptionMigrationService : IEncryptionMigrationService
{
	private static readonly CompositeFormat MigrationFailedForItemFormat =
			CompositeFormat.Parse(Resources.EncryptionMigrationService_MigrationFailedForItemFormat);

	private readonly IEncryptionProvider _encryptionProvider;
	private readonly ILogger<EncryptionMigrationService> _logger;
	private readonly ConcurrentDictionary<string, MigrationStatus> _migrationStatuses = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionMigrationService" /> class.
	/// </summary>
	/// <param name="encryptionProvider"> The encryption provider for encrypt/decrypt operations. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	public EncryptionMigrationService(
		IEncryptionProvider encryptionProvider,
		ILogger<EncryptionMigrationService> logger)
	{
		ArgumentNullException.ThrowIfNull(encryptionProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_encryptionProvider = encryptionProvider;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<EncryptionMigrationResult> MigrateAsync(
		EncryptedData encryptedData,
		EncryptionContext sourceContext,
		EncryptionContext targetContext,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedData);
		ArgumentNullException.ThrowIfNull(sourceContext);
		ArgumentNullException.ThrowIfNull(targetContext);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Decrypt with source context
			var plaintext = await _encryptionProvider.DecryptAsync(
				encryptedData,
				sourceContext,
				cancellationToken).ConfigureAwait(false);

			// Re-encrypt with target context
			var migratedData = await _encryptionProvider.EncryptAsync(
				plaintext,
				targetContext,
				cancellationToken).ConfigureAwait(false);

			stopwatch.Stop();

			LogMigrationSucceeded(encryptedData.KeyId, migratedData.KeyId, stopwatch.Elapsed.TotalMilliseconds);

			return EncryptionMigrationResult.Succeeded(
				migratedData,
				stopwatch.Elapsed,
				encryptedData.KeyId,
				migratedData.KeyId);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			LogMigrationFailed(encryptedData.KeyId, ex);

			return EncryptionMigrationResult.Failed(
				$"Migration failed: {ex.Message}",
				ex,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<EncryptionBatchMigrationResult> MigrateBatchAsync(
		IReadOnlyList<EncryptionMigrationItem> items,
		EncryptionContext targetContext,
		BatchMigrationOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(items);
		ArgumentNullException.ThrowIfNull(targetContext);
		ArgumentNullException.ThrowIfNull(options);

		var migrationId = options.MigrationId ?? Guid.NewGuid().ToString();
		var startedAt = DateTimeOffset.UtcNow;
		var stopwatch = Stopwatch.StartNew();

		var successResults = new ConcurrentDictionary<string, EncryptionMigrationResult>(StringComparer.Ordinal);
		var failureResults = new ConcurrentDictionary<string, EncryptionMigrationResult>(StringComparer.Ordinal);

		// Initialize status tracking
		var status = new MigrationStatus
		{
			MigrationId = migrationId,
			State = MigrationState.Running,
			TotalItems = items.Count,
			CompletedItems = 0,
			SucceededItems = 0,
			FailedItems = 0,
			StartedAt = startedAt,
			LastUpdatedAt = startedAt,
		};
		_migrationStatuses[migrationId] = status;

		LogBatchMigrationStarted(migrationId, items.Count);

		try
		{
			var parallelOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
				CancellationToken = cancellationToken,
			};

			var completedCount = 0;
			var succeededCount = 0;
			var failedCount = 0;

			await Parallel.ForEachAsync(items, parallelOptions, async (item, ct) =>
			{
				using var itemCts = options.ItemTimeout > TimeSpan.Zero
					? new CancellationTokenSource(options.ItemTimeout)
					: new CancellationTokenSource();

				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, itemCts.Token);

				var result = await MigrateAsync(
					item.EncryptedData,
					item.SourceContext,
					targetContext,
					linkedCts.Token).ConfigureAwait(false);

				if (result.Success)
				{
					successResults[item.ItemId] = result;
					_ = Interlocked.Increment(ref succeededCount);
				}
				else
				{
					failureResults[item.ItemId] = result;
					_ = Interlocked.Increment(ref failedCount);

					if (!options.ContinueOnError)
					{
						throw new EncryptionMigrationException(
								string.Format(
										CultureInfo.InvariantCulture,
										MigrationFailedForItemFormat,
										item.ItemId))
						{
							MigrationId = migrationId,
							ItemId = item.ItemId,
						};
					}
				}

				var completed = Interlocked.Increment(ref completedCount);

				// Report progress
				if (options.Progress is not null && options.TrackProgress)
				{
					var avgDuration = stopwatch.Elapsed / completed;
					var remaining = items.Count - completed;

					options.Progress.Report(new EncryptionMigrationProgress
					{
						TotalItems = items.Count,
						CompletedItems = completed,
						SucceededItems = succeededCount,
						FailedItems = failedCount,
						CurrentItemId = item.ItemId,
						Elapsed = stopwatch.Elapsed,
						EstimatedRemaining = avgDuration * remaining,
					});
				}

				// Update status
				_migrationStatuses[migrationId] = status with
				{
					CompletedItems = completed,
					SucceededItems = succeededCount,
					FailedItems = failedCount,
					LastUpdatedAt = DateTimeOffset.UtcNow,
				};
			}).ConfigureAwait(false);

			stopwatch.Stop();
			var completedAt = DateTimeOffset.UtcNow;

			// Update final status
			_migrationStatuses[migrationId] = status with
			{
				State = failedCount == 0 ? MigrationState.Completed : MigrationState.Failed,
				CompletedItems = items.Count,
				SucceededItems = succeededCount,
				FailedItems = failedCount,
				LastUpdatedAt = completedAt,
				CompletedAt = completedAt,
			};

			LogBatchMigrationCompleted(migrationId, succeededCount, failedCount, stopwatch.Elapsed.TotalSeconds);

			return new EncryptionBatchMigrationResult
			{
				Success = failedCount == 0,
				MigrationId = migrationId,
				TotalItems = items.Count,
				SucceededCount = succeededCount,
				FailedCount = failedCount,
				SuccessResults = successResults,
				FailureResults = failureResults,
				Duration = stopwatch.Elapsed,
				StartedAt = startedAt,
				CompletedAt = completedAt,
			};
		}
		catch (OperationCanceledException)
		{
			stopwatch.Stop();
			var cancelledAt = DateTimeOffset.UtcNow;

			_migrationStatuses[migrationId] = status with
			{
				State = MigrationState.Cancelled,
				LastUpdatedAt = cancelledAt,
				CompletedAt = cancelledAt,
				ErrorMessage = "Migration was cancelled",
			};

			LogBatchMigrationCancelled(migrationId);

			return new EncryptionBatchMigrationResult
			{
				Success = false,
				MigrationId = migrationId,
				TotalItems = items.Count,
				SucceededCount = successResults.Count,
				FailedCount = failureResults.Count,
				SuccessResults = successResults,
				FailureResults = failureResults,
				Duration = stopwatch.Elapsed,
				StartedAt = startedAt,
				CompletedAt = cancelledAt,
			};
		}
	}

	/// <inheritdoc />
	public Task<bool> RequiresMigrationAsync(
		EncryptedData encryptedData,
		MigrationPolicy policy,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedData);
		ArgumentNullException.ThrowIfNull(policy);

		var requiresMigration = false;

		// Check deprecated key IDs
		if (policy.DeprecatedKeyIds?.Contains(encryptedData.KeyId) == true)
		{
			requiresMigration = true;
		}

		// Check deprecated algorithms
		if (policy.DeprecatedAlgorithms?.Contains(encryptedData.Algorithm) == true)
		{
			requiresMigration = true;
		}

		// Check target algorithm
		if (policy.TargetAlgorithm.HasValue && encryptedData.Algorithm != policy.TargetAlgorithm.Value)
		{
			requiresMigration = true;
		}

		// Check key age
		if (policy.MaxKeyAge.HasValue)
		{
			var age = DateTimeOffset.UtcNow - encryptedData.EncryptedAt;
			if (age > policy.MaxKeyAge.Value)
			{
				requiresMigration = true;
			}
		}

		// Check encryption date
		if (policy.EncryptedBefore.HasValue && encryptedData.EncryptedAt < policy.EncryptedBefore.Value)
		{
			requiresMigration = true;
		}

		// Check tenant scope
		if (policy.TenantIds is not null &&
			encryptedData.TenantId is not null &&
			!policy.TenantIds.Contains(encryptedData.TenantId))
		{
			// Data is not in scope - doesn't require migration
			requiresMigration = false;
		}

		return Task.FromResult(requiresMigration);
	}

	/// <inheritdoc />
	public Task<MigrationStatus?> GetMigrationStatusAsync(
		string migrationId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(migrationId);

		_ = _migrationStatuses.TryGetValue(migrationId, out var status);
		return Task.FromResult(status);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// This method provides estimation based on the migration policy. For accurate estimates,
	/// call <see cref="EstimateMigrationAsync(MigrationPolicy, int, long, CancellationToken)"/>
	/// with known item count and data size.
	/// </para>
	/// </remarks>
	public Task<MigrationEstimate> EstimateMigrationAsync(
		MigrationPolicy policy,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(policy);

		// Without a data source, return meaningful guidance on how to get accurate estimates
		var warnings = new List<string>
		{
			"No data source specified - cannot determine item count or data size",
			"Use EstimateMigrationAsync(policy, itemCount, dataSizeBytes) for accurate estimates",
		};

		var estimate = new MigrationEstimate
		{
			EstimatedItemCount = 0,
			EstimatedDataSizeBytes = 0,
			EstimatedDuration = TimeSpan.Zero,
			Warnings = warnings,
			EstimatedAt = DateTimeOffset.UtcNow,
		};

		return Task.FromResult(estimate);
	}

	/// <summary>
	/// Estimates the scope and duration of a migration operation with known item count and data size.
	/// </summary>
	/// <param name="policy">The migration policy defining what data requires migration.</param>
	/// <param name="itemCount">The number of items requiring migration.</param>
	/// <param name="dataSizeBytes">The estimated total data size in bytes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>An estimate of the migration scope and duration.</returns>
	/// <remarks>
	/// <para>
	/// Estimation uses the following formula:
	/// <list type="bullet">
	/// <item>Migration time: ~3ms per item (encrypt with AES-256-GCM)</item>
	/// <item>I/O overhead: 20% buffer added to computed duration</item>
	/// <item>Data throughput: ~100 MB/s estimated I/O capacity</item>
	/// </list>
	/// </para>
	/// </remarks>
	public Task<MigrationEstimate> EstimateMigrationAsync(
		MigrationPolicy policy,
		int itemCount,
		long dataSizeBytes,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
		ArgumentOutOfRangeException.ThrowIfNegative(dataSizeBytes);

		if (itemCount == 0)
		{
			return Task.FromResult(new MigrationEstimate
			{
				EstimatedItemCount = 0,
				EstimatedDataSizeBytes = 0,
				EstimatedDuration = TimeSpan.Zero,
				Warnings = ["No items to migrate"],
				EstimatedAt = DateTimeOffset.UtcNow,
			});
		}

		// Calculate estimated duration based on encryption benchmarks
		// Migration: ~3ms per item (encrypt only with AES-256-GCM)
		const double msPerItem = 3.0;
		const double ioOverheadFactor = 1.2; // 20% buffer for I/O operations
		const long ioBytesPerSecond = 100 * 1024 * 1024; // 100 MB/s estimated I/O capacity

		// Time for encryption operations
		var encryptionMs = itemCount * msPerItem;

		// Time for I/O operations
		var ioMs = dataSizeBytes / (double)ioBytesPerSecond * 1000;

		// Total with overhead
		var totalMs = (encryptionMs + ioMs) * ioOverheadFactor;
		var estimatedDuration = TimeSpan.FromMilliseconds(totalMs);

		var warnings = new List<string>();

		// Add warnings based on policy
		if (policy.MaxKeyAge.HasValue && policy.MaxKeyAge.Value.TotalDays < 30)
		{
			warnings.Add($"MaxKeyAge of {policy.MaxKeyAge.Value.TotalDays} days may cause frequent migrations");
		}

		if (estimatedDuration.TotalHours > 1)
		{
			warnings.Add($"Estimated duration exceeds 1 hour - consider batching or scheduling during off-peak hours");
		}

		return Task.FromResult(new MigrationEstimate
		{
			EstimatedItemCount = itemCount,
			EstimatedDataSizeBytes = dataSizeBytes,
			EstimatedDuration = estimatedDuration,
			Warnings = warnings.Count > 0 ? warnings : null,
			EstimatedAt = DateTimeOffset.UtcNow,
		});
	}

	[LoggerMessage(SecurityEventId.EncryptionMigrationSucceeded, LogLevel.Debug, "Migration succeeded from key {SourceKeyId} to {TargetKeyId} in {DurationMs}ms")]
	private partial void LogMigrationSucceeded(string sourceKeyId, string targetKeyId, double durationMs);

	[LoggerMessage(SecurityEventId.EncryptionMigrationFailed, LogLevel.Error, "Migration failed for key {SourceKeyId}")]
	private partial void LogMigrationFailed(string sourceKeyId, Exception ex);

	[LoggerMessage(SecurityEventId.BatchMigrationStarted, LogLevel.Information, "Batch migration {MigrationId} started with {ItemCount} items")]
	private partial void LogBatchMigrationStarted(string migrationId, int itemCount);

	[LoggerMessage(SecurityEventId.BatchMigrationCompleted, LogLevel.Information, "Batch migration {MigrationId} completed: {SucceededCount} succeeded, {FailedCount} failed in {DurationSeconds}s")]
	private partial void LogBatchMigrationCompleted(string migrationId, int succeededCount, int failedCount, double durationSeconds);

	[LoggerMessage(SecurityEventId.BatchMigrationCancelled, LogLevel.Warning, "Batch migration {MigrationId} was cancelled")]
	private partial void LogBatchMigrationCancelled(string migrationId);
}
