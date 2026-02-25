// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Middleware that lazily re-encrypts data when it is accessed if it requires migration.
/// </summary>
/// <remarks>
/// <para>
/// This middleware implements the "lazy migration" pattern for encryption:
/// when encrypted data is accessed, it checks if the data requires migration
/// (e.g., uses a deprecated key or algorithm) and re-encrypts it with the
/// current configuration before processing continues.
/// </para>
/// <para>
/// Lazy re-encryption is useful for gradual migration without requiring
/// a full batch migration upfront.
/// </para>
/// </remarks>
public sealed partial class LazyReEncryptionMiddleware : IDispatchMiddleware
{
	private static readonly CompositeFormat LazyReEncryptionFailedFormat =
			CompositeFormat.Parse(Resources.LazyReEncryptionMiddleware_ReEncryptionFailedFormat);

	private readonly IEncryptionMigrationService _migrationService;
	private readonly LazyReEncryptionOptions _options;
	private readonly ILogger<LazyReEncryptionMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LazyReEncryptionMiddleware" /> class.
	/// </summary>
	/// <param name="migrationService"> The migration service for re-encryption operations. </param>
	/// <param name="encryptionProvider"> The encryption provider. </param>
	/// <param name="options"> The lazy re-encryption options. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	public LazyReEncryptionMiddleware(
		IEncryptionMigrationService migrationService,
		IEncryptionProvider encryptionProvider,
		IOptions<LazyReEncryptionOptions> options,
		ILogger<LazyReEncryptionMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(migrationService);
		ArgumentNullException.ThrowIfNull(encryptionProvider);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_migrationService = migrationService;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if message has encrypted data that needs migration
		if (context.TryGetValue<EncryptedData>("EncryptedPayload", out var encryptedData) &&
			encryptedData is not null)
		{
			var requiresMigration = await _migrationService.RequiresMigrationAsync(
				encryptedData,
				_options.MigrationPolicy,
				cancellationToken).ConfigureAwait(false);

			if (requiresMigration)
			{
				await MigrateEncryptedDataAsync(context, encryptedData, cancellationToken).ConfigureAwait(false);
			}
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	private async Task MigrateEncryptedDataAsync(
		IMessageContext context,
		EncryptedData encryptedData,
		CancellationToken cancellationToken)
	{
		try
		{
			// Build source context from the encrypted data
			var sourceContext = new EncryptionContext
			{
				KeyId = encryptedData.KeyId,
				KeyVersion = encryptedData.KeyVersion,
				TenantId = encryptedData.TenantId,
			};

			// Build target context with current settings
			var targetContext = BuildTargetContext(context);

			var result = await _migrationService.MigrateAsync(
				encryptedData,
				sourceContext,
				targetContext,
				cancellationToken).ConfigureAwait(false);

			if (result.Success && result.MigratedData is not null)
			{
				// Update the context with the migrated data
				context.SetProperty("EncryptedPayload", result.MigratedData);
				context.SetProperty("WasLazilyReEncrypted", value: true);
				context.SetProperty("OriginalKeyId", encryptedData.KeyId);
				context.SetProperty("MigratedKeyId", result.MigratedData.KeyId);

				LogLazyReEncryptionSucceeded(
					encryptedData.KeyId,
					result.MigratedData.KeyId,
					result.Duration.TotalMilliseconds);

				// Notify about re-encryption if callback is configured
				if (_options.OnReEncrypted is not null)
				{
					await _options.OnReEncrypted(encryptedData, result.MigratedData, cancellationToken)
						.ConfigureAwait(false);
				}
			}
			else
			{
				var errorMessage = result.ErrorMessage ?? Resources.LazyReEncryptionMiddleware_UnknownError;
				var migrationException = result.Exception ?? new InvalidOperationException(errorMessage);

				LogLazyReEncryptionFailed(encryptedData.KeyId, errorMessage);

				if (!_options.ContinueOnFailure)
				{
					throw new EncryptionMigrationException(
							string.Format(
									CultureInfo.InvariantCulture,
									LazyReEncryptionFailedFormat,
									errorMessage),
							migrationException)
					{
						ItemId = encryptedData.KeyId,
					};
				}
			}
		}
		catch (Exception ex) when (ex is not EncryptionMigrationException)
		{
			LogLazyReEncryptionError(encryptedData.KeyId, ex);

			if (!_options.ContinueOnFailure)
			{
				throw new EncryptionMigrationException(Resources.LazyReEncryptionMiddleware_ReEncryptionFailed, ex)
				{
					ItemId = encryptedData.KeyId,
				};
			}
		}
	}

	private EncryptionContext BuildTargetContext(IMessageContext messageContext)
	{
		_ = messageContext.TryGetValue<string>("TenantId", out var tenantId);
		_ = messageContext.TryGetValue<string>("EncryptionPurpose", out var purpose);

		return new EncryptionContext
		{
			Algorithm = _options.TargetAlgorithm,
			KeyId = _options.TargetKeyId,
			TenantId = tenantId,
			Purpose = purpose,
		};
	}

	[LoggerMessage(SecurityEventId.LazyReEncryptionSucceeded, LogLevel.Information, "Lazy re-encryption succeeded from key {SourceKeyId} to {TargetKeyId} in {DurationMs}ms")]
	private partial void LogLazyReEncryptionSucceeded(string sourceKeyId, string targetKeyId, double durationMs);

	[LoggerMessage(SecurityEventId.LazyReEncryptionFailed, LogLevel.Warning, "Lazy re-encryption failed for key {SourceKeyId}: {ErrorMessage}")]
	private partial void LogLazyReEncryptionFailed(string sourceKeyId, string errorMessage);

	[LoggerMessage(SecurityEventId.LazyReEncryptionError, LogLevel.Error, "Error during lazy re-encryption for key {SourceKeyId}")]
	private partial void LogLazyReEncryptionError(string sourceKeyId, Exception ex);
}

/// <summary>
/// Options for the lazy re-encryption middleware.
/// </summary>
public sealed class LazyReEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether lazy re-encryption is enabled.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the migration policy that determines when re-encryption is needed.
	/// </summary>
	public MigrationPolicy MigrationPolicy { get; set; } = MigrationPolicy.Default;

	/// <summary>
	/// Gets or sets the target encryption algorithm for re-encrypted data.
	/// </summary>
	public EncryptionAlgorithm? TargetAlgorithm { get; set; }

	/// <summary>
	/// Gets or sets the target key ID for re-encrypted data.
	/// </summary>
	public string? TargetKeyId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to continue processing if re-encryption fails.
	/// </summary>
	public bool ContinueOnFailure { get; set; } = true;

	/// <summary>
	/// Gets or sets an optional callback invoked when data is re-encrypted.
	/// </summary>
	public Func<EncryptedData, EncryptedData, CancellationToken, Task>? OnReEncrypted { get; set; }
}
