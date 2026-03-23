// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IAuditLogEncryptor"/> that encrypts audit log entry
/// fields at rest using the existing key management infrastructure.
/// </summary>
public sealed partial class AuditLogEncryptionService : IAuditLogEncryptor
{
	/// <summary>
	/// AES-GCM nonce size in bytes (96-bit IV required by AES-GCM).
	/// </summary>
	private const int AesGcmIvSizeBytes = 12;

	/// <summary>
	/// AES-GCM authentication tag size in bytes (128-bit tag).
	/// </summary>
	private const int AesGcmAuthTagSizeBytes = 16;

	/// <summary>
	/// Minimum valid envelope size: IV + zero-length ciphertext + AuthTag.
	/// </summary>
	private const int MinEnvelopeSizeBytes = AesGcmIvSizeBytes + AesGcmAuthTagSizeBytes;

	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IKeyManagementProvider _keyManagementProvider;
	private readonly IOptions<AuditLogEncryptionOptions> _options;
	private readonly ILogger<AuditLogEncryptionService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditLogEncryptionService"/> class.
	/// </summary>
	/// <param name="encryptionProvider">The encryption provider.</param>
	/// <param name="keyManagementProvider">The key management provider for key access.</param>
	/// <param name="options">The audit log encryption options.</param>
	/// <param name="logger">The logger.</param>
	public AuditLogEncryptionService(
		IEncryptionProvider encryptionProvider,
		IKeyManagementProvider keyManagementProvider,
		IOptions<AuditLogEncryptionOptions> options,
		ILogger<AuditLogEncryptionService> logger)
	{
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_keyManagementProvider = keyManagementProvider ?? throw new ArgumentNullException(nameof(keyManagementProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<EncryptedAuditEntry> EncryptAsync(
		AuditEvent entry,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(entry);

		var opts = _options.Value;
		var fieldsToEncrypt = opts.EncryptFields;
		var keyId = opts.KeyIdentifier ?? "audit-encryption-key";

		try
		{
			var fieldValues = ExtractFieldValues(entry);
			var encryptedFields = new Dictionary<string, byte[]>();
			var clearFields = new Dictionary<string, string>();

			foreach (var (fieldName, fieldValue) in fieldValues)
			{
				if (fieldValue is null)
				{
					continue;
				}

				if (fieldsToEncrypt.Contains(fieldName))
				{
					var plaintext = Encoding.UTF8.GetBytes(fieldValue);
					var context = new EncryptionContext { KeyId = keyId };
					var encrypted = await _encryptionProvider.EncryptAsync(
						plaintext, context, cancellationToken).ConfigureAwait(false);

					// Envelope format: [IV:12][Ciphertext:N][AuthTag:16]
					var envelope = new byte[encrypted.Iv.Length + encrypted.Ciphertext.Length + encrypted.AuthTag!.Length];
					encrypted.Iv.CopyTo(envelope, 0);
					encrypted.Ciphertext.CopyTo(envelope, encrypted.Iv.Length);
					encrypted.AuthTag.CopyTo(envelope, encrypted.Iv.Length + encrypted.Ciphertext.Length);
					encryptedFields[fieldName] = envelope;
				}
				else
				{
					clearFields[fieldName] = fieldValue;
				}
			}

			LogAuditLogEncryptionCompleted(entry.EventId, encryptedFields.Count);

			return new EncryptedAuditEntry
			{
				EventId = entry.EventId,
				EncryptedFields = encryptedFields,
				ClearFields = clearFields,
				KeyIdentifier = keyId,
				Algorithm = opts.EncryptionAlgorithm
			};
		}
		catch (Exception ex)
		{
			LogAuditLogEncryptionFailed(entry.EventId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<AuditEvent> DecryptAsync(
		EncryptedAuditEntry encryptedEntry,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedEntry);

		try
		{
			var decryptedValues = new Dictionary<string, string>(encryptedEntry.ClearFields);

			foreach (var (fieldName, envelope) in encryptedEntry.EncryptedFields)
			{
				if (envelope.Length < MinEnvelopeSizeBytes)
				{
					throw new EncryptionException(
						$"Encrypted field '{fieldName}' data is too short for envelope format " +
						$"(expected at least {MinEnvelopeSizeBytes} bytes, got {envelope.Length}).");
				}

				// Envelope format: [IV:12][Ciphertext:N][AuthTag:16]
				var iv = envelope.AsSpan(0, AesGcmIvSizeBytes).ToArray();
				var authTag = envelope.AsSpan(envelope.Length - AesGcmAuthTagSizeBytes, AesGcmAuthTagSizeBytes).ToArray();
				var actualCiphertext = envelope.AsSpan(AesGcmIvSizeBytes, envelope.Length - AesGcmIvSizeBytes - AesGcmAuthTagSizeBytes).ToArray();

				var encrypted = new EncryptedData
				{
					Ciphertext = actualCiphertext,
					KeyId = encryptedEntry.KeyIdentifier,
					KeyVersion = 1,
					Algorithm = encryptedEntry.Algorithm,
					Iv = iv,
					AuthTag = authTag
				};

				var context = new EncryptionContext { KeyId = encryptedEntry.KeyIdentifier };
				var plaintext = await _encryptionProvider.DecryptAsync(
					encrypted, context, cancellationToken).ConfigureAwait(false);
				decryptedValues[fieldName] = Encoding.UTF8.GetString(plaintext);
			}

			LogAuditLogDecryptionCompleted(encryptedEntry.EventId);

			return ReconstructAuditEvent(encryptedEntry.EventId, decryptedValues);
		}
		catch (Exception ex)
		{
			LogAuditLogEncryptionFailed(encryptedEntry.EventId, ex);
			throw;
		}
	}

	private static Dictionary<string, string?> ExtractFieldValues(AuditEvent entry) =>
		new()
		{
			[nameof(AuditEvent.EventId)] = entry.EventId,
			[nameof(AuditEvent.EventType)] = entry.EventType.ToString(),
			[nameof(AuditEvent.Action)] = entry.Action,
			[nameof(AuditEvent.Outcome)] = entry.Outcome.ToString(),
			[nameof(AuditEvent.Timestamp)] = entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
			[nameof(AuditEvent.ActorId)] = entry.ActorId,
			[nameof(AuditEvent.ActorType)] = entry.ActorType,
			[nameof(AuditEvent.ResourceId)] = entry.ResourceId,
			[nameof(AuditEvent.ResourceType)] = entry.ResourceType,
			[nameof(AuditEvent.TenantId)] = entry.TenantId,
			[nameof(AuditEvent.CorrelationId)] = entry.CorrelationId,
			[nameof(AuditEvent.IpAddress)] = entry.IpAddress,
			[nameof(AuditEvent.Reason)] = entry.Reason
		};

	private static AuditEvent ReconstructAuditEvent(string eventId, Dictionary<string, string> values) =>
		new()
		{
			EventId = eventId,
			EventType = Enum.TryParse<AuditEventType>(values.GetValueOrDefault(nameof(AuditEvent.EventType)), out var et) ? et : AuditEventType.System,
			Action = values.GetValueOrDefault(nameof(AuditEvent.Action)) ?? string.Empty,
			Outcome = Enum.TryParse<AuditOutcome>(values.GetValueOrDefault(nameof(AuditEvent.Outcome)), out var ao) ? ao : AuditOutcome.Success,
			Timestamp = DateTimeOffset.TryParse(values.GetValueOrDefault(nameof(AuditEvent.Timestamp)), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts) ? ts : DateTimeOffset.UtcNow,
			ActorId = values.GetValueOrDefault(nameof(AuditEvent.ActorId)) ?? string.Empty,
			ActorType = values.GetValueOrDefault(nameof(AuditEvent.ActorType)),
			ResourceId = values.GetValueOrDefault(nameof(AuditEvent.ResourceId)),
			ResourceType = values.GetValueOrDefault(nameof(AuditEvent.ResourceType)),
			TenantId = values.GetValueOrDefault(nameof(AuditEvent.TenantId)),
			CorrelationId = values.GetValueOrDefault(nameof(AuditEvent.CorrelationId)),
			IpAddress = values.GetValueOrDefault(nameof(AuditEvent.IpAddress)),
			Reason = values.GetValueOrDefault(nameof(AuditEvent.Reason))
		};

	[LoggerMessage(
		ComplianceEventId.AuditLogEncryptionCompleted,
		LogLevel.Debug,
		"Audit log entry {EventId} encrypted. Fields encrypted: {FieldCount}")]
	private partial void LogAuditLogEncryptionCompleted(string eventId, int fieldCount);

	[LoggerMessage(
		ComplianceEventId.AuditLogDecryptionCompleted,
		LogLevel.Debug,
		"Audit log entry {EventId} decrypted")]
	private partial void LogAuditLogDecryptionCompleted(string eventId);

	[LoggerMessage(
		ComplianceEventId.AuditLogEncryptionFailed,
		LogLevel.Error,
		"Audit log encryption/decryption failed for entry {EventId}")]
	private partial void LogAuditLogEncryptionFailed(string eventId, Exception exception);
}
