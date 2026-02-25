// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides re-encryption services for key rotation and algorithm migration.
/// </summary>
/// <remarks>
/// <para>
/// This service uses <see cref="IEncryptionProviderRegistry"/> for provider
/// lookup, decrypting with the source provider and encrypting with the target provider.
/// </para>
/// <para>
/// Use cases include:
/// <list type="bullet">
/// <item>Key rotation (migrating to a new encryption key)</item>
/// <item>Algorithm migration (upgrading encryption algorithm)</item>
/// <item>Provider migration (switching encryption providers)</item>
/// </list>
/// </para>
/// </remarks>
public interface IReEncryptionService
{
	/// <summary>
	/// Re-encrypts a single entity with a new provider.
	/// </summary>
	/// <typeparam name="T">The entity type containing encrypted fields.</typeparam>
	/// <param name="entity">The entity to re-encrypt.</param>
	/// <param name="context">The re-encryption context.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The re-encryption result.</returns>
	/// <remarks>
	/// Fields marked with <see cref="EncryptedFieldAttribute"/> are decrypted with the source
	/// provider and re-encrypted with the target provider.
	/// </remarks>
	Task<ReEncryptionResult> ReEncryptAsync<T>(
		T entity,
		ReEncryptionContext context,
		CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Re-encrypts a batch of entities with a new provider using streaming.
	/// </summary>
	/// <typeparam name="T">The entity type containing encrypted fields.</typeparam>
	/// <param name="entities">The entities to re-encrypt.</param>
	/// <param name="options">Options controlling re-encryption behavior.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An async enumerable of re-encryption results.</returns>
	/// <remarks>
	/// <para>
	/// Batch processing uses streaming for memory efficiency.
	/// </para>
	/// <para>
	/// When <see cref="ReEncryptionOptions.ContinueOnError"/> is <c>true</c>,
	/// failed items are yielded with <see cref="ReEncryptionResult.Success"/> = <c>false</c>.
	/// </para>
	/// </remarks>
	IAsyncEnumerable<ReEncryptionResult<T>> ReEncryptBatchAsync<T>(
		IAsyncEnumerable<T> entities,
		ReEncryptionOptions options,
		CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Estimates the work required for a re-encryption operation.
	/// </summary>
	/// <param name="options">Options specifying the scope of re-encryption.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An estimate of the re-encryption work.</returns>
	/// <remarks>
	/// Use this method for planning and progress tracking before executing
	/// <see cref="ReEncryptBatchAsync{T}"/>.
	/// </remarks>
	Task<ReEncryptionEstimate> EstimateAsync(
		ReEncryptionOptions options,
		CancellationToken cancellationToken);
}

/// <summary>
/// Context for a single re-encryption operation.
/// </summary>
public sealed class ReEncryptionContext
{
	/// <summary>
	/// Gets or sets the source provider ID. Null for auto-detect from encrypted data.
	/// </summary>
	/// <remarks>
	/// When <c>null</c>, the appropriate provider is detected from the encrypted data envelope.
	/// </remarks>
	public string? SourceProviderId { get; set; }

	/// <summary>
	/// Gets or sets the target provider ID. Null uses the primary provider.
	/// </summary>
	/// <remarks>
	/// When <c>null</c>, <see cref="IEncryptionProviderRegistry.GetPrimary()"/> is used.
	/// </remarks>
	public string? TargetProviderId { get; set; }

	/// <summary>
	/// Gets or sets the encryption context for both decryption and encryption.
	/// </summary>
	/// <remarks>
	/// Contains purpose, tenant ID, and other contextual information.
	/// </remarks>
	public EncryptionContext? EncryptionContext { get; set; }

	/// <summary>
	/// Gets or sets whether to verify decryption before re-encryption. Default is true.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, the data is decrypted and verified to be valid before re-encryption.
	/// </remarks>
	public bool VerifyBeforeReEncrypt { get; set; } = true;
}

/// <summary>
/// Options for batch re-encryption operations.
/// </summary>
public sealed class ReEncryptionOptions
{
	/// <summary>
	/// Gets or sets the source provider ID. Null for auto-detect from encrypted data.
	/// </summary>
	/// <remarks>
	/// When <c>null</c>, the appropriate provider is detected from the encrypted data envelope.
	/// </remarks>
	public string? SourceProviderId { get; set; }

	/// <summary>
	/// Gets or sets the target provider ID. Null uses the primary provider.
	/// </summary>
	/// <remarks>
	/// When <c>null</c>, <see cref="IEncryptionProviderRegistry.GetPrimary()"/> is used.
	/// </remarks>
	public string? TargetProviderId { get; set; }

	/// <summary>
	/// Gets or sets the batch size for processing. Default is 100.
	/// </summary>
	/// <remarks>
	/// Larger batches improve throughput but increase memory usage.
	/// </remarks>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum degree of parallelism. Default is 4.
	/// </summary>
	/// <remarks>
	/// Set to 1 for sequential processing. Higher values improve throughput
	/// but increase load on the key management system.
	/// </remarks>
	public int MaxDegreeOfParallelism { get; set; } = 4;

	/// <summary>
	/// Gets or sets whether to continue on individual item errors. Default is false.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, failed items are logged and skipped, allowing the operation
	/// to continue. When <c>false</c>, the first error stops processing.
	/// </remarks>
	public bool ContinueOnError { get; set; }

	/// <summary>
	/// Gets or sets whether to verify decryption before re-encryption. Default is true.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, each record is decrypted and verified before re-encryption.
	/// This adds overhead but ensures data integrity.
	/// </remarks>
	public bool VerifyBeforeReEncrypt { get; set; } = true;

	/// <summary>
	/// Gets or sets the encryption context for both decryption and encryption.
	/// </summary>
	/// <remarks>
	/// Contains purpose, tenant ID, and other contextual information.
	/// </remarks>
	public EncryptionContext? Context { get; set; }

	/// <summary>
	/// Gets or sets the timeout for individual item re-encryption. Default is 30 seconds.
	/// </summary>
	public TimeSpan ItemTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Result of a single re-encryption operation.
/// </summary>
public sealed class ReEncryptionResult
{
	/// <summary>
	/// Gets a value indicating whether the re-encryption succeeded.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the source provider ID that was used for decryption.
	/// </summary>
	public string? SourceProviderId { get; init; }

	/// <summary>
	/// Gets the target provider ID that was used for encryption.
	/// </summary>
	public string? TargetProviderId { get; init; }

	/// <summary>
	/// Gets the number of fields that were re-encrypted.
	/// </summary>
	public int FieldsReEncrypted { get; init; }

	/// <summary>
	/// Gets the duration of the re-encryption operation.
	/// </summary>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the exception if the operation failed.
	/// </summary>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Creates a successful result.
	/// </summary>
	public static ReEncryptionResult Succeeded(
		string sourceProviderId,
		string targetProviderId,
		int fieldsReEncrypted,
		TimeSpan duration) =>
		new()
		{
			Success = true,
			SourceProviderId = sourceProviderId,
			TargetProviderId = targetProviderId,
			FieldsReEncrypted = fieldsReEncrypted,
			Duration = duration,
		};

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	public static ReEncryptionResult Failed(string errorMessage, Exception? exception = null) =>
		new()
		{
			Success = false,
			ErrorMessage = errorMessage,
			Exception = exception,
		};
}

/// <summary>
/// Result of a single re-encryption operation with the entity.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class ReEncryptionResult<T> where T : class
{
	/// <summary>
	/// Gets a value indicating whether the re-encryption succeeded.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the entity (re-encrypted if successful, original if failed).
	/// </summary>
	public required T Entity { get; init; }

	/// <summary>
	/// Gets the source provider ID that was used for decryption.
	/// </summary>
	public string? SourceProviderId { get; init; }

	/// <summary>
	/// Gets the target provider ID that was used for encryption.
	/// </summary>
	public string? TargetProviderId { get; init; }

	/// <summary>
	/// Gets the number of fields that were re-encrypted.
	/// </summary>
	public int FieldsReEncrypted { get; init; }

	/// <summary>
	/// Gets the duration of the re-encryption operation.
	/// </summary>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the exception if the operation failed.
	/// </summary>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Creates a successful result.
	/// </summary>
	public static ReEncryptionResult<T> Succeeded(
		T entity,
		string sourceProviderId,
		string targetProviderId,
		int fieldsReEncrypted,
		TimeSpan duration) =>
		new()
		{
			Success = true,
			Entity = entity,
			SourceProviderId = sourceProviderId,
			TargetProviderId = targetProviderId,
			FieldsReEncrypted = fieldsReEncrypted,
			Duration = duration,
		};

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	public static ReEncryptionResult<T> Failed(T entity, string errorMessage, Exception? exception = null) =>
		new()
		{
			Success = false,
			Entity = entity,
			ErrorMessage = errorMessage,
			Exception = exception,
		};
}

/// <summary>
/// Estimate of re-encryption work.
/// </summary>
public sealed class ReEncryptionEstimate
{
	/// <summary>
	/// Gets or sets the estimated number of items requiring re-encryption.
	/// </summary>
	public long EstimatedItemCount { get; init; }

	/// <summary>
	/// Gets or sets the estimated number of encrypted fields per item.
	/// </summary>
	public int EstimatedFieldsPerItem { get; init; }

	/// <summary>
	/// Gets or sets the estimated total duration.
	/// </summary>
	public TimeSpan EstimatedDuration { get; init; }

	/// <summary>
	/// Gets or sets any warnings about the estimate.
	/// </summary>
	public IReadOnlyList<string> Warnings { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the estimate is based on sampling.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, the estimate is based on a sample of the data
	/// rather than a complete scan.
	/// </remarks>
	public bool IsSampled { get; init; }
}
