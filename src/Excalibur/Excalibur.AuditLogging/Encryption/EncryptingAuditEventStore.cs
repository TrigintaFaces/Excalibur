// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Encryption;

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
/// <para>
/// <b>An encrypted field is not searchable, and this decorator says so rather than answering nothing.</b>
/// The cipher beneath is randomized authenticated encryption, so two records holding the same actor id
/// hold different ciphertext, and a server-side <c>=</c> against the caller's plaintext matches no row.
/// A query naming such a field is therefore refused with <see cref="NotSupportedException"/> naming the
/// field — never served as an empty result set, which an operator would read as "this actor did nothing"
/// when the records are present and merely unmatchable.
/// </para>
/// <para>
/// The choice is per field and belongs to the consumer, the same shape a database engine offers when it
/// distinguishes a deterministically-encrypted column (searchable, and equal values are visibly equal to
/// anyone holding the ciphertext) from a randomized one (not searchable, and queries against it fail).
/// Turning off <see cref="AuditEncryptionOptions.EncryptActorId"/> or
/// <see cref="AuditEncryptionOptions.EncryptIpAddress"/> stores that field in the clear and restores the
/// filter; leaving it on keeps the field unreadable at rest at the cost of not being able to filter on it.
/// </para>
/// </remarks>
public sealed class EncryptingAuditEventStore : IAuditStore
{
	/// <summary>
	/// Every <see cref="AuditQuery"/> term this decorator can render unservable, paired with the option
	/// that decides it and the option's name for the message.
	/// </summary>
	/// <remarks>
	/// One table drives both the refusal and what it says, so a field cannot become encrypted in one place
	/// and stay silently filterable in another. A field that is encryptable but carries no
	/// <see cref="AuditQuery"/> term — Reason, UserAgent — has no row here, because there is no filter over
	/// it that could quietly miss.
	/// </remarks>
	private static readonly (string Field, string Option, Func<AuditEncryptionOptions, bool> Encrypted, Func<AuditQuery, string?> Term)[] SearchableEncryptedFields =
	[
		(nameof(AuditQuery.ActorId),
			nameof(AuditEncryptionOptions.EncryptActorId),
			static o => o.EncryptActorId,
			static q => q.ActorId),
		(nameof(AuditQuery.IpAddress),
			nameof(AuditEncryptionOptions.EncryptIpAddress),
			static o => o.EncryptIpAddress,
			static q => q.IpAddress),
	];

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

	/// <summary>
	/// Forwards capability resolution to the wrapped store so that optional capabilities — including
	/// durability (<see cref="IDurableAuditStore"/>) — remain discoverable through this decorator.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve. </param>
	/// <returns> The capability from the wrapped store, or <see langword="null"/> when unavailable. </returns>
	/// <remarks>
	/// A decorator that did not forward would silently disable every capability of the store it wraps —
	/// a durable store behind encryption would report as non-durable. This forward keeps the chain transparent.
	/// </remarks>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return _inner.GetService(serviceType);
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
	/// <exception cref="NotSupportedException">
	/// Thrown when <paramref name="query"/> filters on a field this decorator encrypts. Such a filter
	/// cannot be served and would otherwise return an empty result set indistinguishable from "no such
	/// events". See the remarks on <see cref="EncryptingAuditEventStore"/>.
	/// </exception>
	public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		EnsureEveryFilterIsServable(query);

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
	/// <exception cref="NotSupportedException">
	/// Thrown on the same terms as <see cref="QueryAsync"/>. A count is the more dangerous of the two to
	/// answer wrongly: an unservable filter would report zero, and a zero carries no hint that anything
	/// was withheld.
	/// </exception>
	public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		EnsureEveryFilterIsServable(query);

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

	/// <summary>
	/// Refuses a query that filters on a field this decorator encrypts, before it reaches the store.
	/// </summary>
	/// <param name="query">The query to check.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is null.</exception>
	/// <exception cref="NotSupportedException">
	/// Thrown when a filtered field is encrypted at rest, naming the field and the option that governs it.
	/// </exception>
	/// <remarks>
	/// This runs on the way in, not on the way out, so the caller learns the filter could not be honoured
	/// instead of receiving a result set that was silently unfilterable. Both read members route through
	/// here; a member that did not would reintroduce the empty answer on its own path.
	/// </remarks>
	private void EnsureEveryFilterIsServable(AuditQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		foreach (var (field, option, encrypted, term) in SearchableEncryptedFields)
		{
			if (!encrypted(_options) || string.IsNullOrEmpty(term(query)))
			{
				continue;
			}

			throw new NotSupportedException(
				$"The audit store cannot filter by '{field}' because that field is encrypted at rest. "
				+ "Encryption here is randomized, so two records holding the same value hold different "
				+ "ciphertext and no comparison against the plaintext you supplied can match either of "
				+ "them. Answering this query would return an empty result set that reads as 'no such "
				+ $"events' while the events are present. Set AuditEncryptionOptions.{option} to false to "
				+ $"store '{field}' in the clear and keep it searchable, or filter on a field that is not "
				+ "encrypted.");
		}
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
