// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Encryption;

/// <summary>
/// Provides bulk decryption services for compliance scenarios.
/// </summary>
/// <remarks>
/// This service uses streaming (IAsyncEnumerable) for memory efficiency
/// when processing large datasets for GDPR, audit, or data export requirements.
/// </remarks>
public sealed partial class EncryptionDecryptionService : IEncryptionDecryptionService
{
	private static readonly CompositeFormat UnsupportedExportFormatFormat =
		CompositeFormat.Parse(Resources.EncryptionDecryptionService_UnsupportedExportFormat);

	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly ILogger<EncryptionDecryptionService> _logger;

	/// <summary>
	/// Resolves the tenant of the operation in flight. Consulted PER CALL, never captured, so the tenant
	/// bound into the AES-GCM Additional Authenticated Data is the tenant of the data rather than a
	/// process-wide constant.
	/// </summary>
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionDecryptionService"/> class.
	/// </summary>
	/// <param name="registry">The encryption provider registry.</param>
	/// <param name="options">The encryption configuration options.</param>
	/// <param name="tenantContext">
	/// Resolves the tenant of the operation in flight, so the tenant bound into the authenticated data is
	/// the tenant of the data rather than a process-wide constant. A caller decrypting records that belong
	/// to a different tenant than the ambient one supplies <see cref="DecryptionOptions.Context"/> instead.
	/// </param>
	/// <param name="logger">The logger for diagnostics.</param>
	public EncryptionDecryptionService(
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options,
		ITenantContext tenantContext,
		ILogger<EncryptionDecryptionService> logger)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<T> DecryptAllAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		IAsyncEnumerable<T> source,
		DecryptionOptions options,
		[EnumeratorCancellation] CancellationToken cancellationToken) where T : class
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(options);

		var mode = _options.Value.Mode;
		if (mode == EncryptionMode.Disabled)
		{
			await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				yield return item;
			}

			yield break;
		}

		var batch = new List<T>(options.BatchSize);
		var processedCount = 0;
		var errorCount = 0;

		await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			batch.Add(item);

			if (batch.Count >= options.BatchSize)
			{
				var (decryptedBatch, batchErrors) = await DecryptBatchAsync(batch, options, cancellationToken).ConfigureAwait(false);
				errorCount += batchErrors;
				foreach (var decrypted in decryptedBatch)
				{
					processedCount++;
					yield return decrypted;
				}

				batch.Clear();
			}
		}

		// Process remaining items
		if (batch.Count > 0)
		{
			var (decryptedBatch, batchErrors) = await DecryptBatchAsync(batch, options, cancellationToken).ConfigureAwait(false);
			errorCount += batchErrors;
			foreach (var decrypted in decryptedBatch)
			{
				processedCount++;
				yield return decrypted;
			}
		}

		LogDecryptAllCompleted(processedCount, errorCount);
	}

	/// <inheritdoc/>
	public async Task<T> DecryptEntityAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		T entity,
		DecryptionOptions options,
		CancellationToken cancellationToken) where T : class
	{
		ArgumentNullException.ThrowIfNull(entity);
		ArgumentNullException.ThrowIfNull(options);

		var mode = _options.Value.Mode;
		if (mode == EncryptionMode.Disabled)
		{
			return entity;
		}

		var context = options.Context ?? CreateDefaultContext();
		var encryptedProperties = GetDecryptableProperties<T>(out _);

		foreach (var prop in encryptedProperties)
		{
			if (!EncryptedFieldBinding.TryReadEnvelope(prop, entity, out var value))
			{
				continue;
			}

			{
				try
				{
					var decrypted = await DecryptFieldAsync(value, options, context, cancellationToken).ConfigureAwait(false);
					EncryptedFieldBinding.WritePlaintext(prop, entity, decrypted);
				}
				catch (Exception ex) when (options.ContinueOnError)
				{
					LogDecryptionError(prop.Name, ex);
				}
			}
		}

		return entity;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode(
		"Exporting decrypted data uses JSON serialization for generic types which requires preserved members.")]
	[RequiresDynamicCode(
		"Exporting decrypted data uses JSON serialization for generic types which requires dynamic code generation.")]
	public async Task ExportDecryptedAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		IAsyncEnumerable<T> source,
		BulkDecryptionExportOptions options,
		CancellationToken cancellationToken) where T : class
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(options);

		var decryptionOptions = new DecryptionOptions
		{
			BatchSize = options.BatchSize,
			ContinueOnError = options.ContinueOnError,
			Context = options.Context,
			IncludeUnencryptedFields = true
		};

		var exportedCount = 0;

		switch (options.Format)
		{
			case DecryptionExportFormat.Json:
				await ExportAsJsonAsync(source, options, decryptionOptions, cancellationToken).ConfigureAwait(false);
				break;

			case DecryptionExportFormat.Csv:
				await ExportAsCsvAsync(source, options, decryptionOptions, cancellationToken).ConfigureAwait(false);
				break;

			case DecryptionExportFormat.Plaintext:
				await ExportAsPlaintextAsync(source, options, decryptionOptions, cancellationToken).ConfigureAwait(false);
				break;

			default:
				throw new ArgumentException(
					string.Format(
						CultureInfo.InvariantCulture,
						UnsupportedExportFormatFormat,
						options.Format),
					nameof(options));
		}

		LogExportCompleted(options.Format.ToString(), exportedCount);
	}

	private static string EscapeCsvField(string field)
	{
		if (field.Contains(',', StringComparison.Ordinal) ||
			field.Contains('"', StringComparison.Ordinal) ||
			field.Contains('\n', StringComparison.Ordinal) ||
			field.Contains('\r', StringComparison.Ordinal))
		{
			return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
		}

		return field;
	}

	/// <summary>
	/// Selects the annotated properties for a DECRYPT path, reporting any annotation that cannot be
	/// honoured instead of refusing the whole record.
	/// </summary>
	/// <remarks>
	/// Decrypting asks a different question from encrypting, and the two have opposite correct answers.
	/// Encrypting must REFUSE an unhonourable annotation, because proceeding would leave the field in
	/// plaintext while the developer believes otherwise. Decrypting must not: the annotation denies this
	/// framework the ability to write that property, so refusing protects nothing and costs the caller
	/// every OTHER field on the record, which decrypts perfectly.
	/// <para>
	/// It is reported rather than skipped in silence, because "always read-only" is not the only way a
	/// property reaches this state. One that HAD a setter, stored ciphertext under it, and lost the setter
	/// in a later release still holds that ciphertext — and handing it back unannounced would return
	/// ciphertext where the application expects a value, with nothing to indicate it happened.
	/// </para>
	/// </remarks>
	private PropertyInfo[] GetDecryptableProperties<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		out int unhonouredCount)
	{
		var properties = EncryptedFieldBinding.Inspect(typeof(T), out var unhonourable);
		foreach (var reason in unhonourable)
		{
			LogEncryptedFieldNotHonouredOnRead(typeof(T).Name, reason);
		}

		unhonouredCount = unhonourable.Count;
		return properties;
	}

	private static EncryptedData DeserializeEncryptedData(byte[] data)
	{
		var envelopeData = data.AsSpan(EncryptedData.MagicBytes.Length);
		return JsonSerializer.Deserialize(
				   envelopeData,
				   EncryptionJsonContext.Default.EncryptedData)
			   ?? throw new EncryptionException(Resources.Encryption_EncryptedDataEnvelopeDeserializeFailed);
	}

	private async Task<(List<T> Results, int Errors)> DecryptBatchAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		List<T> batch,
		DecryptionOptions options,
		CancellationToken cancellationToken) where T : class
	{
		var results = new List<T>(batch.Count);
		var errors = 0;
		var context = options.Context ?? CreateDefaultContext();
		var encryptedProperties = GetDecryptableProperties<T>(out var unhonouredCount);
		errors += unhonouredCount;

		foreach (var entity in batch)
		{
			try
			{
				foreach (var prop in encryptedProperties)
				{
					if (!EncryptedFieldBinding.TryReadEnvelope(prop, entity, out var value))
					{
						continue;
					}

					var decrypted = await DecryptFieldAsync(value, options, context, cancellationToken).ConfigureAwait(false);
					EncryptedFieldBinding.WritePlaintext(prop, entity, decrypted);
				}

			}
			catch (Exception ex) when (options.ContinueOnError)
			{
				// The entity is added below regardless. Dropping it would remove a record from an export
				// that still reports success, which is a worse outcome than returning it with one field
				// undecrypted: the caller can see a field it cannot read, and cannot see a record absent.
				LogDecryptionError(typeof(T).Name, ex);
				errors++;
			}

			results.Add(entity);
		}

		return (results, errors);
	}

	private async Task<byte[]> DecryptFieldAsync(
		byte[] data,
		DecryptionOptions options,
		EncryptionContext context,
		CancellationToken cancellationToken)
	{
		var encryptedData = DeserializeEncryptedData(data);

		IEncryptionProvider? provider;
		if (options.ProviderId is not null)
		{
			provider = _registry.GetProvider(options.ProviderId);
		}
		else
		{
			provider = _registry.FindDecryptionProvider(encryptedData);
		}

		if (provider is null)
		{
			throw new EncryptionException(Resources.Encryption_NoProviderCanDecrypt);
		}

		return await provider.DecryptAsync(encryptedData, context, cancellationToken).ConfigureAwait(false);
	}

	[RequiresDynamicCode("JSON serialization for generic types requires dynamic code generation.")]
	[RequiresUnreferencedCode("JSON serialization for generic types requires preserved members.")]
	private async Task ExportAsJsonAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		IAsyncEnumerable<T> source,
		BulkDecryptionExportOptions options,
		DecryptionOptions decryptionOptions,
		CancellationToken cancellationToken) where T : class
	{
		await using var writer = new StreamWriter(options.Destination, Encoding.UTF8, leaveOpen: true);
		var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

		await writer.WriteLineAsync("[").ConfigureAwait(false);
		var first = true;

		await foreach (var entity in DecryptAllAsync(source, decryptionOptions, cancellationToken).ConfigureAwait(false))
		{
			if (!first)
			{
				await writer.WriteLineAsync(",").ConfigureAwait(false);
			}

			first = false;

			var json = JsonSerializer.Serialize(entity, jsonOptions);
			await writer.WriteAsync(json).ConfigureAwait(false);
		}

		await writer.WriteLineAsync().ConfigureAwait(false);
		await writer.WriteLineAsync("]").ConfigureAwait(false);
	}

	private async Task ExportAsCsvAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		IAsyncEnumerable<T> source,
		BulkDecryptionExportOptions options,
		DecryptionOptions decryptionOptions,
		CancellationToken cancellationToken) where T : class
	{
		await using var writer = new StreamWriter(options.Destination, Encoding.UTF8, leaveOpen: true);

		var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanRead)
			.ToArray();

		// Write header
		var header = string.Join(",", properties.Select(p => EscapeCsvField(p.Name)));
		await writer.WriteLineAsync(header).ConfigureAwait(false);

		// Write rows
		await foreach (var entity in DecryptAllAsync(source, decryptionOptions, cancellationToken).ConfigureAwait(false))
		{
			var values = properties.Select(p =>
			{
				var value = p.GetValue(entity);
				return value switch
				{
					null => "",
					byte[] bytes => Convert.ToBase64String(bytes),
					_ => value.ToString() ?? ""
				};
			});
			var line = string.Join(",", values.Select(EscapeCsvField));
			await writer.WriteLineAsync(line).ConfigureAwait(false);
		}
	}

	[RequiresDynamicCode("JSON serialization for generic types requires dynamic code generation.")]
	[RequiresUnreferencedCode("JSON serialization for generic types requires preserved members.")]
	private async Task ExportAsPlaintextAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		IAsyncEnumerable<T> source,
		BulkDecryptionExportOptions options,
		DecryptionOptions decryptionOptions,
		CancellationToken cancellationToken) where T : class
	{
		await using var writer = new StreamWriter(options.Destination, Encoding.UTF8, leaveOpen: true);

		await foreach (var entity in DecryptAllAsync(source, decryptionOptions, cancellationToken).ConfigureAwait(false))
		{
			var json = JsonSerializer.Serialize(entity);
			await writer.WriteLineAsync(json).ConfigureAwait(false);
		}
	}

	private EncryptionContext CreateDefaultContext()
	{
		return new EncryptionContext
		{
			Purpose = _options.Value.DefaultPurpose,
			TenantId = _tenantContext.TenantId,
			RequireFipsCompliance = _options.Value.RequireFipsCompliance
		};
	}

	// Source-generated logging methods
	[LoggerMessage(ComplianceEventId.BulkDecryptionCompleted, LogLevel.Information,
		"Bulk decryption completed: {ProcessedCount} items processed, {ErrorCount} errors")]
	private partial void LogDecryptAllCompleted(int processedCount, int errorCount);

	[LoggerMessage(ComplianceEventId.DecryptionErrorForField, LogLevel.Warning, "Decryption error for field/entity {FieldName}")]
	private partial void LogDecryptionError(string fieldName, Exception ex);

	[LoggerMessage(ComplianceEventId.EncryptedFieldNotHonouredOnRead, LogLevel.Warning,
		"[EncryptedField] on {TypeName} could not be honoured while decrypting, so the property was left as stored: {Reason}")]
	private partial void LogEncryptedFieldNotHonouredOnRead(string typeName, string reason);

	[LoggerMessage(ComplianceEventId.ExportCompleted, LogLevel.Information, "Export completed: {Format} format, {ExportedCount} items")]
	private partial void LogExportCompleted(string format, int exportedCount);
}
