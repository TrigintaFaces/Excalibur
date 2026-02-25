// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides bulk decryption services for compliance scenarios (GDPR, audit, data export).
/// </summary>
/// <remarks>
/// <para>
/// This interface supports streaming decryption for memory efficiency
/// when processing large datasets for compliance requirements.
/// </para>
/// <para>
/// Use cases include:
/// <list type="bullet">
/// <item>GDPR data portability requests</item>
/// <item>Audit trail exports</item>
/// <item>Data migration to external systems</item>
/// <item>Backup verification</item>
/// </list>
/// </para>
/// </remarks>
public interface IEncryptionDecryptionService
{
	/// <summary>
	/// Decrypts all encrypted fields in a collection using streaming for memory efficiency.
	/// </summary>
	/// <typeparam name="T">The entity type containing encrypted fields.</typeparam>
	/// <param name="source">The source collection of entities to decrypt.</param>
	/// <param name="options">Options controlling decryption behavior.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An async enumerable of decrypted entities.</returns>
	/// <remarks>
	/// <para>
	/// Entities are processed in batches (per <see cref="DecryptionOptions.BatchSize"/>)
	/// and yielded as they are decrypted to minimize memory usage.
	/// </para>
	/// <para>
	/// Fields marked with <see cref="EncryptedFieldAttribute"/> are decrypted;
	/// other fields are passed through unchanged.
	/// </para>
	/// </remarks>
	IAsyncEnumerable<T> DecryptAllAsync<T>(
		IAsyncEnumerable<T> source,
		DecryptionOptions options,
		CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Decrypts encrypted fields in a single entity.
	/// </summary>
	/// <typeparam name="T">The entity type containing encrypted fields.</typeparam>
	/// <param name="entity">The entity to decrypt.</param>
	/// <param name="options">Options controlling decryption behavior.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The decrypted entity (same instance with fields decrypted).</returns>
	/// <remarks>
	/// Fields marked with <see cref="EncryptedFieldAttribute"/> are decrypted in place.
	/// </remarks>
	Task<T> DecryptEntityAsync<T>(
		T entity,
		DecryptionOptions options,
		CancellationToken cancellationToken) where T : class;

	/// <summary>
	/// Exports decrypted data to a stream for compliance purposes.
	/// </summary>
	/// <typeparam name="T">The entity type to export.</typeparam>
	/// <param name="source">The source collection of entities to decrypt and export.</param>
	/// <param name="options">Options controlling export behavior.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the export operation.</returns>
	/// <remarks>
	/// <para>
	/// Supports multiple export formats (JSON, CSV, Plaintext) for compliance flexibility.
	/// </para>
	/// <para>
	/// Data is streamed to the destination to support large exports without
	/// loading the entire dataset into memory.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode(
			"Exporting decrypted data uses JSON serialization for generic types which requires preserved members.")]
	[RequiresDynamicCode(
			"Exporting decrypted data uses JSON serialization for generic types which requires dynamic code generation.")]
	Task ExportDecryptedAsync<T>(
			IAsyncEnumerable<T> source,
			BulkDecryptionExportOptions options,
			CancellationToken cancellationToken) where T : class;
}

/// <summary>
/// Options for bulk decryption operations.
/// </summary>
public sealed class DecryptionOptions
{
	/// <summary>
	/// Gets or sets the batch size for processing. Default is 100.
	/// </summary>
	/// <remarks>
	/// Larger batches improve throughput but increase memory usage.
	/// </remarks>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets whether to include non-encrypted fields in output.
	/// </summary>
	/// <remarks>
	/// When <c>true</c> (default), the entire entity is returned with encrypted fields decrypted.
	/// When <c>false</c>, only encrypted field values are included.
	/// </remarks>
	public bool IncludeUnencryptedFields { get; set; } = true;

	/// <summary>
	/// Gets or sets the provider ID to use for decryption, or null for auto-detect.
	/// </summary>
	/// <remarks>
	/// When <c>null</c>, the appropriate provider is detected from the encrypted data envelope.
	/// </remarks>
	public string? ProviderId { get; set; }

	/// <summary>
	/// Gets or sets whether to continue processing on decryption errors.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, failed items are skipped and logged.
	/// When <c>false</c> (default), the first error stops processing.
	/// </remarks>
	public bool ContinueOnError { get; set; }

	/// <summary>
	/// Gets or sets the encryption context for decryption.
	/// </summary>
	/// <remarks>
	/// Required for multi-tenant scenarios or when specific purposes are used.
	/// </remarks>
	public EncryptionContext? Context { get; set; }
}

/// <summary>
/// Options for bulk decryption export operations.
/// </summary>
public sealed class BulkDecryptionExportOptions
{
	/// <summary>
	/// Gets or sets the destination stream for exported data.
	/// </summary>
	public required Stream Destination { get; init; }

	/// <summary>
	/// Gets or sets the export format. Default is JSON.
	/// </summary>
	public DecryptionExportFormat Format { get; set; } = DecryptionExportFormat.Json;

	/// <summary>
	/// Gets or sets whether to include metadata in the export.
	/// </summary>
	/// <remarks>
	/// When <c>true</c> (default), export includes timestamp and entity type information.
	/// </remarks>
	public bool IncludeMetadata { get; set; } = true;

	/// <summary>
	/// Gets or sets the batch size for export processing. Default is 100.
	/// </summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets whether to continue exporting on errors.
	/// </summary>
	public bool ContinueOnError { get; set; }

	/// <summary>
	/// Gets or sets the encryption context for decryption.
	/// </summary>
	public EncryptionContext? Context { get; set; }
}

/// <summary>
/// Specifies the format for bulk decryption export.
/// </summary>
/// <remarks>
/// Used by <see cref="IEncryptionDecryptionService"/> for bulk decryption exports.
/// Not to be confused with <see cref="Soc2.ExportFormat"/> for SOC 2 reports.
/// </remarks>
public enum DecryptionExportFormat
{
	/// <summary>
	/// Export as JSON format (default).
	/// </summary>
	Json,

	/// <summary>
	/// Export as CSV format.
	/// </summary>
	Csv,

	/// <summary>
	/// Export as plaintext with one record per line.
	/// </summary>
	Plaintext
}
