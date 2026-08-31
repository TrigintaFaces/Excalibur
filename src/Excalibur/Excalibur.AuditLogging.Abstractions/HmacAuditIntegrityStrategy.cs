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
	public async ValueTask<AuditChainVerificationResult> VerifyChainAsync(
		IAsyncEnumerable<AuditChainLink> chain,
		string? anchorPriorTag,
		AuditChainLink? successor,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(chain);

		// The fold carries exactly one accumulator — the prior tag — plus the index used to report where a
		// break was found. Nothing else is retained, so the space cost does not grow with the range.
		//
		// Seeded from the anchor, so the first in-range link is bound to the record preceding the range
		// rather than being treated as a genesis record. Without this, truncating the front of a range is
		// indistinguishable from verifying a range that legitimately starts at genesis.
		var priorTag = anchorPriorTag;
		var index = 0;

		await foreach (var link in chain.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			// An untagged record cannot be a link. Reported rather than skipped, because skipping it would
			// let clearing a record's tag stand in for deleting the record.
			if (string.IsNullOrEmpty(link.Tag))
			{
				return new AuditChainVerificationResult(false, index, AuditChainBreak.UntaggedRecord);
			}

			var macVerified = await VerifyAsync(link.CanonicalContent, priorTag, link.Tag, cancellationToken).ConfigureAwait(false);

			// The record's own claim about its predecessor, against the predecessor actually present. The MAC
			// does not cover this value — it covers the prior tag supplied at write time, not the copy stored
			// in the row — so it is the one part of a link an attacker can move without moving a MAC input.
			// Comparing it here is what makes it a verified value rather than an unread one.
			var claimMatchesPredecessor = TagsEqual(link.StoredPriorTag, priorTag);

			if (!macVerified)
			{
				// The MAC covers the record's contents and its predecessor's tag together, so a mismatch alone
				// does not say which of the two moved. The stored claim separates them, and the distinction is
				// what a reader needs: a rewritten record and a missing one call for different responses.
				return new AuditChainVerificationResult(
					false,
					index,
					claimMatchesPredecessor ? AuditChainBreak.ContentAltered : AuditChainBreak.PredecessorMismatch);
			}

			if (!claimMatchesPredecessor)
			{
				return new AuditChainVerificationResult(false, index, AuditChainBreak.StoredLinkageAltered);
			}

			priorTag = link.Tag;
			index++;
		}

		// The right edge, pinned the way the anchor pins the left. The successor was written to follow the
		// range's last record, so its keyed MAC is bound to a tag that changes the moment records are removed
		// from the end of the range. Without this the right edge is pinned by nothing at all: the survivors
		// chain perfectly to one another and to the anchor, and nothing inside the range mentions the removed
		// suffix, so there is nothing left to detect. The successor is the only record that still carries it.
		if (successor is { } tail)
		{
			if (string.IsNullOrEmpty(tail.Tag))
			{
				return new AuditChainVerificationResult(false, index, AuditChainBreak.UntaggedRecord);
			}

			var tailVerified = await VerifyAsync(tail.CanonicalContent, priorTag, tail.Tag, cancellationToken)
				.ConfigureAwait(false);

			if (!tailVerified)
			{
				return new AuditChainVerificationResult(false, index, AuditChainBreak.SuccessorLinkBroken);
			}
		}

		return new AuditChainVerificationResult(true, -1, AuditChainBreak.None);
	}

	// A tag column that holds no tag means the same thing however a backend spells absence: a record claiming
	// to be its partition's genesis. Comparing the two spellings as distinct values would report an intact
	// trail broken on any store that writes an empty string where another writes null.
	private static bool TagsEqual(string? left, string? right) =>
		string.Equals(
			string.IsNullOrEmpty(left) ? null : left,
			string.IsNullOrEmpty(right) ? null : right,
			StringComparison.Ordinal);

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
