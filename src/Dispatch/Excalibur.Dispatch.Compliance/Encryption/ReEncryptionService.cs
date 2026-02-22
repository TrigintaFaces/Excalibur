// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Compliance.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides re-encryption services for key rotation and algorithm migration.
/// </summary>
/// <remarks>
/// This service uses <see cref="IEncryptionProviderRegistry"/> for provider
/// lookup, decrypting with the source provider and encrypting with the target provider.
/// </remarks>
public sealed partial class ReEncryptionService : IReEncryptionService
{
	private static readonly CompositeFormat SourceProviderNotFoundFormat =
		CompositeFormat.Parse(Resources.ReEncryptionService_SourceProviderNotFound);

	private static readonly CompositeFormat TargetProviderNotFoundFormat =
		CompositeFormat.Parse(Resources.ReEncryptionService_TargetProviderNotFound);

	private readonly IEncryptionProviderRegistry _registry;
	private readonly ILogger<ReEncryptionService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ReEncryptionService"/> class.
	/// </summary>
	/// <param name="registry">The encryption provider registry.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	public ReEncryptionService(
		IEncryptionProviderRegistry registry,
		ILogger<ReEncryptionService> logger)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<ReEncryptionResult> ReEncryptAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		T entity,
		ReEncryptionContext context,
		CancellationToken cancellationToken) where T : class
	{
		ArgumentNullException.ThrowIfNull(entity);
		ArgumentNullException.ThrowIfNull(context);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			var encryptedProperties = GetEncryptedProperties<T>();
			var fieldsReEncrypted = 0;
			string? sourceProviderId = null;
			string? targetProviderId = null;

			foreach (var prop in encryptedProperties)
			{
				var value = (byte[]?)prop.GetValue(entity);
				if (value is null || value.Length == 0)
				{
					continue;
				}

				if (!EncryptedData.IsFieldEncrypted(value))
				{
					continue;
				}

				var (reEncrypted, source, target) = await ReEncryptFieldAsync(
					value,
					context,
					cancellationToken).ConfigureAwait(false);

				prop.SetValue(entity, reEncrypted);
				fieldsReEncrypted++;
				sourceProviderId ??= source;
				targetProviderId ??= target;
			}

			if (fieldsReEncrypted == 0)
			{
				return ReEncryptionResult.Succeeded(
					sourceProviderId ?? "none",
					targetProviderId ?? "none",
					fieldsReEncrypted: 0,
					stopwatch.Elapsed);
			}

			LogReEncryptionSucceeded(fieldsReEncrypted, stopwatch.Elapsed.TotalMilliseconds);

			return ReEncryptionResult.Succeeded(
				sourceProviderId ?? "unknown",
				targetProviderId ?? "unknown",
				fieldsReEncrypted,
				stopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			LogReEncryptionFailed(ex);
			return ReEncryptionResult.Failed(ex.Message, ex);
		}
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<ReEncryptionResult<T>> ReEncryptBatchAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		IAsyncEnumerable<T> entities,
		ReEncryptionOptions options,
		[EnumeratorCancellation] CancellationToken cancellationToken) where T : class
	{
		ArgumentNullException.ThrowIfNull(entities);
		ArgumentNullException.ThrowIfNull(options);

		var context = new ReEncryptionContext
		{
			SourceProviderId = options.SourceProviderId,
			TargetProviderId = options.TargetProviderId,
			EncryptionContext = options.Context,
			VerifyBeforeReEncrypt = options.VerifyBeforeReEncrypt,
		};

		var batch = new List<T>(options.BatchSize);
		var processedCount = 0;

		await foreach (var entity in entities.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			batch.Add(entity);

			if (batch.Count >= options.BatchSize)
			{
				foreach (var result in await ProcessBatchAsync(batch, context, options, cancellationToken).ConfigureAwait(false))
				{
					processedCount++;
					yield return result;
				}

				batch.Clear();
			}
		}

		// Process remaining items
		if (batch.Count > 0)
		{
			foreach (var result in await ProcessBatchAsync(batch, context, options, cancellationToken).ConfigureAwait(false))
			{
				processedCount++;
				yield return result;
			}
		}

		LogBatchReEncryptionCompleted(processedCount);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// This method provides estimation based on available information. For accurate estimates,
	/// call <see cref="EstimateForTypeAsync{T}"/> with a known entity type and item count.
	/// </para>
	/// </remarks>
	public Task<ReEncryptionEstimate> EstimateAsync(
		ReEncryptionOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Without a specific entity type and data source, we cannot provide accurate estimates.
		// Return a meaningful result indicating the limitation and how to get better estimates.
		var warnings = new List<string>
		{
			"No entity type specified - cannot detect encrypted field count",
			"No data source specified - cannot determine item count",
			"Use EstimateForTypeAsync<T>(long itemCount) for accurate estimates based on entity type",
		};

		var estimate = new ReEncryptionEstimate
		{
			EstimatedItemCount = 0,
			EstimatedFieldsPerItem = 0,
			EstimatedDuration = TimeSpan.Zero,
			IsSampled = false,
			Warnings = warnings,
		};

		return Task.FromResult(estimate);
	}

	/// <summary>
	/// Estimates re-encryption work for a specific entity type with known item count.
	/// </summary>
	/// <typeparam name="T">The entity type containing encrypted fields.</typeparam>
	/// <param name="itemCount">The number of items to re-encrypt.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>An estimate of re-encryption work.</returns>
	/// <remarks>
	/// <para>
	/// Estimation uses the following formula:
	/// <list type="bullet">
	/// <item>Re-encryption time: ~5ms per field (decrypt + encrypt with AES-256-GCM)</item>
	/// <item>I/O overhead: 20% buffer added to computed duration</item>
	/// </list>
	/// </para>
	/// </remarks>
	public Task<ReEncryptionEstimate> EstimateForTypeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		long itemCount,
		CancellationToken cancellationToken) where T : class
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentOutOfRangeException.ThrowIfNegative(itemCount);

		// Detect encrypted fields on the entity type
		var encryptedProps = GetEncryptedProperties<T>();
		var fieldsPerItem = encryptedProps.Length;

		if (fieldsPerItem == 0)
		{
			return Task.FromResult(new ReEncryptionEstimate
			{
				EstimatedItemCount = itemCount,
				EstimatedFieldsPerItem = 0,
				EstimatedDuration = TimeSpan.Zero,
				IsSampled = false,
				Warnings = [$"No encrypted fields found on entity type {typeof(T).Name}"],
			});
		}

		// Calculate estimated duration based on encryption benchmarks
		// Re-encryption: ~5ms per field (decrypt AES-256-GCM + encrypt AES-256-GCM)
		const double msPerFieldReEncryption = 5.0;
		const double ioOverheadFactor = 1.2; // 20% buffer for I/O operations

		var totalFields = itemCount * fieldsPerItem;
		var estimatedMs = totalFields * msPerFieldReEncryption * ioOverheadFactor;
		var estimatedDuration = TimeSpan.FromMilliseconds(estimatedMs);

		var fieldNames = encryptedProps.Select(p => p.Name).ToList();

		return Task.FromResult(new ReEncryptionEstimate
		{
			EstimatedItemCount = itemCount,
			EstimatedFieldsPerItem = fieldsPerItem,
			EstimatedDuration = estimatedDuration,
			IsSampled = false,
			Warnings = [],
		});
	}

	private static PropertyInfo[] GetEncryptedProperties<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>()
	{
		return [.. typeof(T)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType == typeof(byte[]) &&
						p.GetCustomAttribute<EncryptedFieldAttribute>() is not null &&
						p.CanRead && p.CanWrite)];
	}

	private static EncryptedData DeserializeEncryptedData(byte[] data)
	{
		var envelopeData = data.AsSpan(EncryptedData.MagicBytes.Length);
		return JsonSerializer.Deserialize(
				   envelopeData,
				   EncryptionJsonContext.Default.EncryptedData)
			   ?? throw new EncryptionException(Resources.Encryption_EncryptedDataEnvelopeDeserializeFailed);
	}

	private static byte[] SerializeEncryptedData(EncryptedData encryptedData)
	{
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(
			encryptedData,
			EncryptionJsonContext.Default.EncryptedData);
		var result = new byte[EncryptedData.MagicBytes.Length + jsonBytes.Length];
		EncryptedData.MagicBytes.CopyTo(result.AsSpan());
		jsonBytes.CopyTo(result.AsSpan(EncryptedData.MagicBytes.Length));
		return result;
	}

	private async Task<IEnumerable<ReEncryptionResult<T>>> ProcessBatchAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		List<T> batch,
		ReEncryptionContext context,
		ReEncryptionOptions options,
		CancellationToken cancellationToken) where T : class
	{
		var results = new List<ReEncryptionResult<T>>(batch.Count);

		foreach (var entity in batch)
		{
			try
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				cts.CancelAfter(options.ItemTimeout);

				var result = await ReEncryptAsync(entity, context, cts.Token).ConfigureAwait(false);

				results.Add(result.Success
					? ReEncryptionResult<T>.Succeeded(
						entity,
						result.SourceProviderId ?? "unknown",
						result.TargetProviderId ?? "unknown",
						result.FieldsReEncrypted,
						result.Duration)
					: ReEncryptionResult<T>.Failed(entity, result.ErrorMessage ?? "Unknown error", result.Exception));
			}
			catch (Exception ex) when (options.ContinueOnError)
			{
				LogReEncryptionItemFailed(typeof(T).Name, ex);
				results.Add(ReEncryptionResult<T>.Failed(entity, ex.Message, ex));
			}
		}

		return results;
	}

	private async Task<(byte[] reEncrypted, string sourceId, string targetId)> ReEncryptFieldAsync(
		byte[] data,
		ReEncryptionContext context,
		CancellationToken cancellationToken)
	{
		// Deserialize the encrypted data envelope
		var encryptedData = DeserializeEncryptedData(data);

		// Determine source provider ID
		var sourceProviderId = context.SourceProviderId ?? encryptedData.Algorithm.ToString();

		// Find source provider
		IEncryptionProvider sourceProvider;
		if (context.SourceProviderId is not null)
		{
			sourceProvider = _registry.GetProvider(context.SourceProviderId)
							 ?? throw new EncryptionException(
								 string.Format(
									 CultureInfo.InvariantCulture,
									 SourceProviderNotFoundFormat,
									 context.SourceProviderId));
		}
		else
		{
			sourceProvider = _registry.FindDecryptionProvider(encryptedData)
							 ?? throw new EncryptionException(
								 Resources.Encryption_NoProviderCanDecrypt);
		}

		// Determine target provider ID
		var targetProviderId = context.TargetProviderId ?? "primary";

		// Find target provider
		IEncryptionProvider targetProvider;
		if (context.TargetProviderId is not null)
		{
			targetProvider = _registry.GetProvider(context.TargetProviderId)
							 ?? throw new EncryptionException(
								 string.Format(
									 CultureInfo.InvariantCulture,
									 TargetProviderNotFoundFormat,
									 context.TargetProviderId));
		}
		else
		{
			targetProvider = _registry.GetPrimary()
							 ?? throw new EncryptionException(
								 Resources.Encryption_NoPrimaryProviderRegistered);
		}

		// Skip if source and target provider IDs are the same and the key hasn't changed
		if (context.SourceProviderId == context.TargetProviderId &&
			encryptedData.KeyId == (context.EncryptionContext?.KeyId ?? encryptedData.KeyId))
		{
			// Already using the target provider and key - no re-encryption needed
			return (data, sourceProviderId, targetProviderId);
		}

		// Decrypt with source provider
		var encryptionContext = context.EncryptionContext ?? new EncryptionContext
		{
			KeyId = encryptedData.KeyId,
			KeyVersion = encryptedData.KeyVersion,
			TenantId = encryptedData.TenantId,
		};

		var plaintext = await sourceProvider.DecryptAsync(encryptedData, encryptionContext, cancellationToken).ConfigureAwait(false);

		// Verify if requested (data is valid if we got here without exception)
		if (context.VerifyBeforeReEncrypt && plaintext.Length == 0)
		{
			throw new EncryptionException(Resources.ReEncryptionService_EmptyPlaintext);
		}

		// Encrypt with target provider
		var reEncryptedData = await targetProvider.EncryptAsync(plaintext, encryptionContext, cancellationToken).ConfigureAwait(false);

		// Serialize the new encrypted data
		var result = SerializeEncryptedData(reEncryptedData);

		return (result, sourceProviderId, targetProviderId);
	}

	// Source-generated logging methods (Sprint 369 - EventId migration)
	[LoggerMessage(ComplianceEventId.ReEncryptionSucceeded, LogLevel.Information,
		"Re-encryption succeeded: {FieldCount} fields in {DurationMs}ms")]
	private partial void LogReEncryptionSucceeded(int fieldCount, double durationMs);

	[LoggerMessage(ComplianceEventId.ReEncryptionFailed, LogLevel.Warning, "Re-encryption failed")]
	private partial void LogReEncryptionFailed(Exception ex);

	[LoggerMessage(ComplianceEventId.ReEncryptionFailedForItem, LogLevel.Warning,
		"Re-encryption failed for item of type {EntityType}")]
	private partial void LogReEncryptionItemFailed(string entityType, Exception ex);

	[LoggerMessage(ComplianceEventId.BatchReEncryptionCompleted, LogLevel.Information,
		"Batch re-encryption completed: {ProcessedCount} items")]
	private partial void LogBatchReEncryptionCompleted(int processedCount);
}
