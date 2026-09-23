// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Globalization;
using System.Security.Cryptography;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// The keys an erasure's key-management provider irreversibly scheduled for destruction rather than destroyed,
/// with the destruction instant each one reported. Each such key has also registered an error; this set exists
/// only so the outcome can distinguish "scheduled, awaiting the provider" from a genuine failure.
/// </summary>
internal sealed class ScheduledKeyDestructions
{
	private readonly List<string> _keyIds = [];

	/// <summary>Gets the number of scheduled keys.</summary>
	public int Count => _keyIds.Count;

	/// <summary>Gets the latest destruction instant any provider reported, or <see langword="null"/> if none did.</summary>
	public DateTimeOffset? LatestIrreversibleAt { get; private set; }

	/// <summary>Records a key the provider scheduled for irreversible destruction.</summary>
	public void Add(string keyId, DateTimeOffset? irreversibleAt)
	{
		_keyIds.Add(keyId);
		if (irreversibleAt.HasValue && (LatestIrreversibleAt is null || irreversibleAt > LatestIrreversibleAt))
		{
			LatestIrreversibleAt = irreversibleAt;
		}
	}

	/// <summary>
	/// Gets the destroyed keys together with the scheduled ones, for the coverage evaluation: a location protected
	/// by a scheduled key is covered pending its destruction, which the scheduled-key error still gates.
	/// </summary>
	public IReadOnlyCollection<string> WithDestroyed(IReadOnlyCollection<string> destroyedKeyIds) =>
		_keyIds.Count == 0 ? destroyedKeyIds : [.. destroyedKeyIds, .. _keyIds];

	/// <summary>
	/// The certificate id for a request completed by confirmation: a pure function of the request id, so every
	/// confirmer of the same request names the same certificate. An identifier, not key material.
	/// </summary>
	public static Guid ConfirmationCertificateId(Guid requestId)
	{
		var label = "excalibur-erasure-confirmation-certificate"u8;
		Span<byte> input = stackalloc byte[16 + label.Length];
		_ = requestId.TryWriteBytes(input);
		label.CopyTo(input[16..]);
		Span<byte> hash = stackalloc byte[32];
		_ = SHA256.HashData(input, hash);
		hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // RFC 9562 version 5-style (name-based, SHA-derived)
		hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
		return new Guid(hash[..16]);
	}

	/// <summary>Describes the waiting state for operators, including the provider-reported time.</summary>
	public string Describe(int keysDestroyedNow, int recordsAffected)
	{
		var scheduledKeys = this;
		var latest = LatestIrreversibleAt;
		var when = latest.HasValue
			? $"the latest provider-reported destruction time is {latest.Value.ToString("O", CultureInfo.InvariantCulture)}"
			: "the provider did not report a destruction time";

		return $"Awaiting the key-management provider's destruction of {scheduledKeys.Count} key(s) it has "
			+ $"irreversibly scheduled for deletion; {when}. That time is informational: the request reaches "
			+ "Completed, with its certificate, only when the provider confirms the keys are destroyed. At execution "
			+ $"{keysDestroyedNow} key(s) were destroyed immediately and erasure contributors affected "
			+ $"{recordsAffected} record(s).";
	}
}
