// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Encryption;

/// <summary>
/// Delegating decorator that encrypts sensitive fields on audit events before storage
/// and decrypts them on retrieval.
/// </summary>
/// <remarks>
/// <para>
/// This decorator wraps any <see cref="IAuditStore"/> implementation and transparently
/// encrypts/decrypts configurable fields (ActorId, IpAddress, Reason, UserAgent) using
/// the registered <see cref="IEncryptionProvider"/>.
/// </para>
/// <para>
/// Encrypted fields are stored as Base64-encoded <see cref="EncryptedData"/> JSON,
/// preserving all key metadata needed for decryption. The original field format
/// is restored on retrieval via <see cref="GetByIdAsync"/> and <see cref="QueryAsync"/>.
/// </para>
/// <para>
/// Fields that are <see langword="null"/> or empty are not encrypted.
/// </para>
/// </remarks>
public sealed class EncryptingAuditEventStore : IAuditStore
{
	private readonly IAuditStore _inner;
	private readonly IEncryptionProvider _encryption;
	private readonly AuditEncryptionOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingAuditEventStore"/> class.
	/// </summary>
	/// <param name="inner">The inner audit store to delegate to.</param>
	/// <param name="encryption">The encryption provider.</param>
	/// <param name="options">The encryption options controlling which fields are encrypted.</param>
	public EncryptingAuditEventStore(
		IAuditStore inner,
		IEncryptionProvider encryption,
		IOptions<AuditEncryptionOptions> options)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc />
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		var encrypted = await EncryptFieldsAsync(auditEvent, cancellationToken).ConfigureAwait(false);
		return await _inner.StoreAsync(encrypted, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		var result = await _inner.GetByIdAsync(eventId, cancellationToken).ConfigureAwait(false);
		if (result is null)
		{
			return null;
		}

		return await DecryptFieldsAsync(result, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		var results = await _inner.QueryAsync(query, cancellationToken).ConfigureAwait(false);
		if (results.Count == 0)
		{
			return results;
		}

		var decrypted = new List<AuditEvent>(results.Count);
		foreach (var evt in results)
		{
			decrypted.Add(await DecryptFieldsAsync(evt, cancellationToken).ConfigureAwait(false));
		}

		return decrypted;
	}

	/// <inheritdoc />
	public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		return _inner.CountAsync(query, cancellationToken);
	}

	/// <inheritdoc />
	public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		return _inner.VerifyChainIntegrityAsync(startDate, endDate, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		var result = await _inner.GetLastEventAsync(tenantId, cancellationToken).ConfigureAwait(false);
		if (result is null)
		{
			return null;
		}

		return await DecryptFieldsAsync(result, cancellationToken).ConfigureAwait(false);
	}

	private async Task<AuditEvent> EncryptFieldsAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		var context = new EncryptionContext
		{
			TenantId = auditEvent.TenantId,
			Purpose = _options.EncryptionPurpose,
		};

		var actorId = auditEvent.ActorId;
		var ipAddress = auditEvent.IpAddress;
		var reason = auditEvent.Reason;
		var userAgent = auditEvent.UserAgent;

		if (_options.EncryptActorId && !string.IsNullOrEmpty(actorId))
		{
			actorId = await EncryptStringAsync(actorId, context, cancellationToken).ConfigureAwait(false);
		}

		if (_options.EncryptIpAddress && !string.IsNullOrEmpty(ipAddress))
		{
			ipAddress = await EncryptStringAsync(ipAddress, context, cancellationToken).ConfigureAwait(false);
		}

		if (_options.EncryptReason && !string.IsNullOrEmpty(reason))
		{
			reason = await EncryptStringAsync(reason, context, cancellationToken).ConfigureAwait(false);
		}

		if (_options.EncryptUserAgent && !string.IsNullOrEmpty(userAgent))
		{
			userAgent = await EncryptStringAsync(userAgent, context, cancellationToken).ConfigureAwait(false);
		}

		return auditEvent with
		{
			ActorId = actorId,
			IpAddress = ipAddress,
			Reason = reason,
			UserAgent = userAgent,
		};
	}

	private async Task<AuditEvent> DecryptFieldsAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		var context = new EncryptionContext
		{
			TenantId = auditEvent.TenantId,
			Purpose = _options.EncryptionPurpose,
		};

		var actorId = auditEvent.ActorId;
		var ipAddress = auditEvent.IpAddress;
		var reason = auditEvent.Reason;
		var userAgent = auditEvent.UserAgent;

		if (_options.EncryptActorId && !string.IsNullOrEmpty(actorId))
		{
			actorId = await DecryptStringAsync(actorId, context, cancellationToken).ConfigureAwait(false);
		}

		if (_options.EncryptIpAddress && !string.IsNullOrEmpty(ipAddress))
		{
			ipAddress = await DecryptStringAsync(ipAddress, context, cancellationToken).ConfigureAwait(false);
		}

		if (_options.EncryptReason && !string.IsNullOrEmpty(reason))
		{
			reason = await DecryptStringAsync(reason, context, cancellationToken).ConfigureAwait(false);
		}

		if (_options.EncryptUserAgent && !string.IsNullOrEmpty(userAgent))
		{
			userAgent = await DecryptStringAsync(userAgent, context, cancellationToken).ConfigureAwait(false);
		}

		return auditEvent with
		{
			ActorId = actorId,
			IpAddress = ipAddress,
			Reason = reason,
			UserAgent = userAgent,
		};
	}

	private async Task<string> EncryptStringAsync(string plaintext, EncryptionContext context, CancellationToken cancellationToken)
	{
		var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
		var encrypted = await _encryption.EncryptAsync(plaintextBytes, context, cancellationToken).ConfigureAwait(false);

		// Serialize the full EncryptedData to JSON so we preserve key metadata for decryption
		var json = JsonSerializer.Serialize(encrypted, AuditEncryptionJsonContext.Default.EncryptedData);
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
	}

	private async Task<string> DecryptStringAsync(string encodedCiphertext, EncryptionContext context, CancellationToken cancellationToken)
	{
		var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCiphertext));
		var encryptedData = JsonSerializer.Deserialize(json, AuditEncryptionJsonContext.Default.EncryptedData);
		if (encryptedData is null)
		{
			return encodedCiphertext;
		}

		var decrypted = await _encryption.DecryptAsync(encryptedData, context, cancellationToken).ConfigureAwait(false);
		return Encoding.UTF8.GetString(decrypted);
	}
}
