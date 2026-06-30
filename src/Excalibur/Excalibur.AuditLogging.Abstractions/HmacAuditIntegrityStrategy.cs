// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Excalibur.AuditLogging;

/// <summary>
/// The canonical <see cref="IAuditIntegrityStrategy"/>: a keyed HMAC-SHA256 over the record's canonical
/// content chained to the prior record's tag, producing a versioned <c>v1:{keyId}:{mac}</c> tag.
/// </summary>
/// <remarks>
/// There is no unkeyed path: every tag is produced with a key from <see cref="IAuditSigningKeyProvider"/>,
/// and a missing key fails closed (compute throws; verify reports invalid). Verification uses a
/// constant-time comparison.
/// </remarks>
internal sealed class HmacAuditIntegrityStrategy : IAuditIntegrityStrategy
{
	private const string TagVersion = "v1";
	private const int NullPriorTagSentinel = -1;

	private readonly IAuditSigningKeyProvider _keyProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="HmacAuditIntegrityStrategy"/> class.
	/// </summary>
	/// <param name="keyProvider">The provider of the keyed-MAC signing key.</param>
	public HmacAuditIntegrityStrategy(IAuditSigningKeyProvider keyProvider)
	{
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
	}

	/// <inheritdoc />
	public async ValueTask<string> ComputeTagAsync(ReadOnlyMemory<byte> canonicalContent, string? priorTag, CancellationToken cancellationToken)
	{
		// GetCurrentSigningKeyAsync fails closed (throws) when no key is available — never an unkeyed tag.
		var (keyId, key) = await _keyProvider.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false);
		if (key is null || key.Length == 0)
		{
			throw new InvalidOperationException("Audit signing key is unavailable; cannot compute a keyed integrity tag.");
		}

		if (keyId.Contains(':', StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"Audit signing key id '{keyId}' must be colon-free (the integrity tag is colon-delimited).");
		}

		var mac = ComputeMac(key, canonicalContent.Span, priorTag);
		return string.Concat(TagVersion, ":", keyId, ":", Convert.ToBase64String(mac));
	}

	/// <inheritdoc />
	public async ValueTask<bool> VerifyAsync(ReadOnlyMemory<byte> canonicalContent, string? priorTag, string tag, CancellationToken cancellationToken)
	{
		if (!TryParseTag(tag, out var keyId, out var expectedMac))
		{
			return false; // malformed / wrong version => unverifiable, never valid.
		}

		var key = await _keyProvider.GetSigningKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (key is null || key.Length == 0)
		{
			return false; // unknown / unavailable key => fail closed.
		}

		var actualMac = ComputeMac(key, canonicalContent.Span, priorTag);
		return CryptographicOperations.FixedTimeEquals(actualMac, expectedMac);
	}

	/// <inheritdoc />
	public async ValueTask<AuditChainVerificationResult> VerifyChainAsync(IReadOnlyList<AuditChainLink> chain, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(chain);

		string? priorTag = null;
		for (var i = 0; i < chain.Count; i++)
		{
			var link = chain[i];
			var verified = await VerifyAsync(link.CanonicalContent, priorTag, link.Tag, cancellationToken).ConfigureAwait(false);
			if (!verified)
			{
				return new AuditChainVerificationResult(false, i);
			}

			priorTag = link.Tag;
		}

		return new AuditChainVerificationResult(true, -1);
	}

	// MAC input = canonicalContent ‖ length-prefixed(priorTag). The length prefix keeps the
	// content/priorTag boundary unambiguous so chain linkage cannot be forged by shifting bytes.
	private static byte[] ComputeMac(byte[] key, ReadOnlySpan<byte> canonicalContent, string? priorTag)
	{
		var priorTagByteCount = priorTag is null ? 0 : Encoding.UTF8.GetByteCount(priorTag);
		var buffer = new byte[canonicalContent.Length + 4 + priorTagByteCount];

		canonicalContent.CopyTo(buffer);
		var offset = canonicalContent.Length;

		if (priorTag is null)
		{
			BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset, 4), NullPriorTagSentinel);
		}
		else
		{
			BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset, 4), priorTagByteCount);
			_ = Encoding.UTF8.GetBytes(priorTag, 0, priorTag.Length, buffer, offset + 4);
		}

		return HMACSHA256.HashData(key, buffer);
	}

	private static bool TryParseTag(string tag, out string keyId, out byte[] mac)
	{
		keyId = string.Empty;
		mac = [];

		if (string.IsNullOrEmpty(tag))
		{
			return false;
		}

		var parts = tag.Split(':');
		if (parts.Length != 3 || !string.Equals(parts[0], TagVersion, StringComparison.Ordinal) || parts[1].Length == 0)
		{
			return false;
		}

		Span<byte> decoded = stackalloc byte[32];
		if (!Convert.TryFromBase64String(parts[2], decoded, out var written) || written != 32)
		{
			return false; // not a well-formed HMAC-SHA256 tag.
		}

		keyId = parts[1];
		mac = decoded[..written].ToArray();
		return true;
	}
}
