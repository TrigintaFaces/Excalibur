// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

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
	/// Initializes a new instance of the <see cref="EncryptionDecryptionService"/> class.
	/// </summary>
	/// <param name="registry">The encryption provider registry.</param>
	/// <param name="options">The encryption configuration options.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	public EncryptionDecryptionService(
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options,
		ILogger<EncryptionDecryptionService> logger)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
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
				foreach (var decrypted in await DecryptBatchAsync(batch, options, cancellationToken).ConfigureAwait(false))
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
			foreach (var decrypted in await DecryptBatchAsync(batch, options, cancellationToken).ConfigureAwait(false))
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
		var encryptedProperties = GetEncryptedProperties<T>();

		foreach (var prop in encryptedProperties)
		{
			var value = (byte[]?)prop.GetValue(entity);
			if (value is null || value.Length == 0)
			{
				continue;
			}

			if (EncryptedData.IsFieldEncrypted(value))
			{
				try
				{
					var decrypted = await DecryptFieldAsync(value, options, context, cancellationToken).ConfigureAwait(false);
					prop.SetValue(entity, decrypted);
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

	private async Task<IEnumerable<T>> DecryptBatchAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
		List<T> batch,
		DecryptionOptions options,
		CancellationToken cancellationToken) where T : class
	{
		var results = new List<T>(batch.Count);
		var context = options.Context ?? CreateDefaultContext();
		var encryptedProperties = GetEncryptedProperties<T>();

		foreach (var entity in batch)
		{
			try
			{
				foreach (var prop in encryptedProperties)
				{
					var value = (byte[]?)prop.GetValue(entity);
					if (value is null || value.Length == 0)
					{
						continue;
					}

					if (EncryptedData.IsFieldEncrypted(value))
					{
						var decrypted = await DecryptFieldAsync(value, options, context, cancellationToken).ConfigureAwait(false);
						prop.SetValue(entity, decrypted);
					}
				}

				results.Add(entity);
			}
			catch (Exception ex) when (options.ContinueOnError)
			{
				LogDecryptionError(typeof(T).Name, ex);
			}
		}

		return results;
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
			TenantId = _options.Value.DefaultTenantId,
			RequireFipsCompliance = _options.Value.RequireFipsCompliance
		};
	}

	// Source-generated logging methods (Sprint 369 - EventId migration)
	[LoggerMessage(ComplianceEventId.BulkDecryptionCompleted, LogLevel.Information,
		"Bulk decryption completed: {ProcessedCount} items processed, {ErrorCount} errors")]
	private partial void LogDecryptAllCompleted(int processedCount, int errorCount);

	[LoggerMessage(ComplianceEventId.DecryptionErrorForField, LogLevel.Warning, "Decryption error for field/entity {FieldName}")]
	private partial void LogDecryptionError(string fieldName, Exception ex);

	[LoggerMessage(ComplianceEventId.ExportCompleted, LogLevel.Information, "Export completed: {Format} format, {ExportedCount} items")]
	private partial void LogExportCompleted(string format, int exportedCount);
}
