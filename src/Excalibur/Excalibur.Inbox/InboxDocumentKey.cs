// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

namespace Excalibur.Inbox;

/// <summary>
/// Composes the single-string deduplication key used by document stores whose inbox entry is addressed by
/// one document id rather than by a composite primary key. The terms are caller data, so the composition
/// has to be <em>injective</em>: distinct <c>(tenant, message, handler)</c> triples must never render the
/// same key. Inputs on which that cannot be guaranteed are rejected, never silently merged.
/// </summary>
/// <remarks>
/// <para>
/// A plain join is not injective, and the failure is silent. Joining with a separator that can also occur
/// <em>inside</em> a term makes the rendering ambiguous: tenant <c>acme</c> with message <c>corp_42</c>
/// and tenant <c>acme_corp</c> with message <c>42</c> both render <c>acme_corp_42</c>. This is the
/// deduplication key, so the collision never surfaces as an error — the second message reads as already
/// seen and is dropped. An inbox exists to not drop messages, and it fails here on the success path, where
/// nothing prompts an investigation.
/// </para>
/// <para>
/// Injectivity has two halves, and they are established differently. Against the <strong>separator</strong>
/// it is structural: each term is percent-encoded with <see cref="Uri.EscapeDataString(string)"/>, whose
/// output alphabet is the RFC 3986 unreserved set plus percent triples — a set that provably excludes
/// <c>':'</c> — so the separator cannot occur inside an encoded term and the split points are unambiguous.
/// Against the <strong>encoder</strong> it is guarded rather than structural, because
/// <see cref="Uri.EscapeDataString(string)"/> is <em>not</em> injective over all of
/// <see cref="string"/>: an unpaired surrogate is neither rejected nor preserved but substituted with
/// U+FFFD, so <c>"a\uD800"</c>, <c>"a\uDC00"</c> and <c>"a\uFFFD"</c> all encode to
/// <c>"a%EF%BF%BD"</c>. Those inputs are therefore refused here. Over the domain this accepts — text that
/// is well-formed UTF-16 — the composition is injective and reverses with
/// <see cref="Uri.UnescapeDataString(string)"/>.
/// </para>
/// <para>
/// The encoder is the framework's rather than ours on purpose. A hand-written escape that replaces the
/// separator and the escape character has to apply them in one specific order to stay reversible, and
/// nothing in the language enforces that order — a later edit that swaps the two lines reintroduces
/// collisions and still passes a casual reading. The escape set here is fixed by the standard and by the
/// runtime, so there is no ordering to get wrong.
/// </para>
/// <para>
/// The rendered key is not stable across a change of encoding: adopting this composition moves existing
/// documents to new ids. That is a one-time data migration for the store, not something the read path
/// compensates for at runtime.
/// </para>
/// </remarks>
internal static class InboxDocumentKey
{
	/// <summary>
	/// Renders the deduplication key for one inbox entry.
	/// </summary>
	/// <param name="tenantId">
	/// The tenant term the entry belongs to. Always concrete — an untenanted deployment supplies the
	/// framework's reserved sentinel — so the key is tenant-discriminated in every deployment and two
	/// tenants carrying the same message id never resolve to one document.
	/// </param>
	/// <param name="messageId">The message identifier being deduplicated.</param>
	/// <param name="handlerType">The handler the message is being deduplicated for.</param>
	/// <returns>A key that is equal for equal triples and distinct for distinct ones.</returns>
	/// <exception cref="ArgumentException">
	/// A term is <see langword="null"/>, empty, whitespace, or contains an unpaired surrogate.
	/// </exception>
	internal static string Compose(string tenantId, string messageId, string handlerType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		ThrowIfNotWellFormed(tenantId, nameof(tenantId));
		ThrowIfNotWellFormed(messageId, nameof(messageId));
		ThrowIfNotWellFormed(handlerType, nameof(handlerType));

		return $"{Uri.EscapeDataString(tenantId)}:{Uri.EscapeDataString(messageId)}:{Uri.EscapeDataString(handlerType)}";
	}

	/// <summary>
	/// Verifies a composed document id fits the addressing limit of the store it is destined for, failing
	/// with a diagnosable message rather than letting the provider reject the write with a generic one.
	/// </summary>
	/// <remarks>
	/// Percent-encoding expands: a term made of characters outside the unreserved set grows by up to three
	/// bytes per character, and a handler type name that is a nested closed generic measures roughly 1.24×
	/// its unencoded length. A store with a short id limit can therefore refuse an id whose unencoded terms
	/// would have fitted, so the limit is checked here where the cause is still visible. It is checked and
	/// never truncated: a truncated key is a key two different messages can share, which is the defect this
	/// whole type exists to prevent.
	/// </remarks>
	/// <param name="documentId">The composed id.</param>
	/// <param name="maxUtf8Bytes">The store's maximum document-id length, in UTF-8 bytes.</param>
	/// <param name="storeName">The store, named in the exception so the limit is attributable.</param>
	/// <exception cref="ArgumentException">The id exceeds <paramref name="maxUtf8Bytes"/>.</exception>
	internal static void ThrowIfExceedsIdLimit(string documentId, int maxUtf8Bytes, string storeName)
	{
		var actual = Encoding.UTF8.GetByteCount(documentId);

		if (actual <= maxUtf8Bytes)
		{
			return;
		}

		throw new ArgumentException(
			$"The composed inbox document id is {actual} UTF-8 bytes, which exceeds the {maxUtf8Bytes}-byte "
			+ $"document id limit of {storeName}. The id is built from the tenant, message id, and handler "
			+ "type, each percent-encoded; encoding expands characters outside A-Z a-z 0-9 - . _ ~, so a "
			+ "handler type name that is a nested closed generic grows by roughly a quarter. Shorten the "
			+ "handler type name used for deduplication, or the message id. The id is never truncated to "
			+ "fit, because two different messages could then share one key.",
			nameof(documentId));
	}

	/// <summary>
	/// Rejects a term that is not well-formed UTF-16. An unpaired surrogate is not text; the encoder maps
	/// every one of them onto the same replacement character, so admitting them would let two different
	/// messages share a deduplication key and silently drop the second.
	/// </summary>
	private static void ThrowIfNotWellFormed(string value, string paramName)
	{
		var remaining = value.AsSpan();

		while (!remaining.IsEmpty)
		{
			if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done)
			{
				throw new ArgumentException(
					"The value contains an unpaired surrogate and is not well-formed text. It cannot be part "
					+ "of a deduplication key: percent-encoding maps every unpaired surrogate onto the same "
					+ "replacement character, so two different values would render one key and the second "
					+ "message would be dropped as a duplicate it is not.",
					paramName);
			}

			remaining = remaining[consumed..];
		}
	}
}
