// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// The framework's single injective encoding for composing several identity terms into one string key.
/// </summary>
/// <remarks>
/// <para>
/// Joining terms with a bare separator — <c>tenant + ":" + id</c> — is not injective, because no term is
/// validated against any charset. A term containing the separator shifts across the boundary and two
/// distinct tuples render one key:
/// </para>
/// <code>
/// tenant "acme"     record "eu:order-42"   ->  acme:eu:order-42
/// tenant "acme:eu"  record "order-42"      ->  acme:eu:order-42     IDENTICAL
/// </code>
/// <para>
/// Two identities addressing one key is not a formatting problem. Depending on what the key addresses it
/// is one tenant reading, overwriting or irreversibly erasing another's records; one tenant's cached
/// response served to another; or one tenant's authorization grant answering for another's request.
/// </para>
/// <para>
/// <b>'%' is escaped FIRST, and that is what makes the encoding reversible.</b> Escaping only ':' would map
/// the distinct terms <c>a:b</c> and <c>a%3Ab</c> onto one key — a collision introduced by the escaping
/// itself.
/// </para>
/// <para>
/// <b>The encoding is the IDENTITY on any term containing neither ':' nor '%'</b>, which is what makes it
/// safe to adopt on already-persisted keys: an ordinary identifier encodes to itself, byte for byte, so no
/// stored record is orphaned. The only keys whose bytes change are the ambiguous ones, which had no single
/// correct owner to begin with.
/// </para>
/// <para>
/// <b>Why not <see cref="System.Uri.EscapeDataString(string)" />.</b> The framework's standing direction is to build
/// on the BCL rather than hand-roll an equivalent, and <c>EscapeDataString</c> is injective, so it was the
/// first candidate. It is rejected for one measured reason: it percent-encodes everything outside the RFC
/// 3986 unreserved set <c>A-Z a-z 0-9 - . _ ~</c>, so it is <em>not</em> the identity on ordinary
/// identifiers. A tenant term containing a space, '+', '@' or any non-ASCII character encodes to different
/// bytes under <c>EscapeDataString</c> than the bytes already stored, which turns a free, migration-free
/// consolidation into a rewrite of shipped keys. The identity property above is the whole reason this
/// encoding can be adopted on persisted data, and no BCL escaper preserves it. That gap is asserted by a
/// test rather than left as a claim here.
/// </para>
/// <para>
/// <b>This type exists so the encoding has ONE implementation, reachable from both frameworks.</b> It had
/// grown three times independently — once per subsystem that needed it — because the existing copies sat in
/// packages the other subsystems could not reference. It lives here because this package is the only one
/// every affected subsystem already reaches, and it takes no dependency of its own: the file is pure BCL.
/// </para>
/// </remarks>
public static class SegmentedKey
{
	/// <summary>
	/// The separator placed between encoded segments. Segments are encoded so that none can contain it.
	/// </summary>
	public const char Separator = ':';

	private const char EscapeChar = '%';

	private const string SeparatorText = ":";

	/// <summary>
	/// Encodes one term so that no character in it can be mistaken for a segment boundary.
	/// </summary>
	/// <param name="value">The term to encode.</param>
	/// <returns>
	/// The term with '%' and ':' escaped — the same instance as <paramref name="value" /> when it contains
	/// neither.
	/// </returns>
	/// <remarks>
	/// Exposed because subsystems compose different shapes — some join a tenant with one identifier, others
	/// with a user, a grant type and a qualifier. Each shape stays injective as long as every term goes
	/// through here and the separator between them is a raw <see cref="Separator" />.
	/// </remarks>
	/// <exception cref="System.ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
	public static string Escape(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		// ONE PASS, DELIBERATELY. The obvious form is
		//     value.Replace("%", "%25").Replace(":", "%3A")
		// which is correct ONLY in that order, and nothing but a comment enforces it: an edit that swaps
		// the two lines re-introduces the collision this type exists to remove -- "a:b" and "a%3Ab" would
		// both render "a%3Ab" -- and still reads fine. Encoding each character once, here, leaves no
		// ordering for a later edit to get wrong.
		var needed = 0;

		foreach (var c in value)
		{
			if (c is EscapeChar or Separator)
			{
				needed++;
			}
		}

		if (needed == 0)
		{
			// The identity case, and the common one: no allocation, and the bytes a consumer already has
			// stored are reproduced exactly. This runs on the authorization path, where a key is composed
			// per access check.
			return value;
		}

		return string.Create(value.Length + (needed * 2), value, static (destination, source) =>
		{
			var at = 0;

			foreach (var c in source)
			{
				switch (c)
				{
					case EscapeChar:
						"%25".CopyTo(destination[at..]);
						at += 3;
						break;
					case Separator:
						"%3A".CopyTo(destination[at..]);
						at += 3;
						break;
					default:
						destination[at++] = c;
						break;
				}
			}
		});
	}

	/// <summary>
	/// Reverses <see cref="Escape" />.
	/// </summary>
	/// <param name="value">The encoded term.</param>
	/// <returns>The original term.</returns>
	/// <remarks>
	/// ':' is unescaped BEFORE '%', mirroring the encode order. Doing it the other way round would turn an
	/// encoded <c>%253A</c> — a literal "%3A" in the original term — into a separator that was never there.
	/// </remarks>
	/// <exception cref="System.ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
	public static string Unescape(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return value.IndexOf(EscapeChar, StringComparison.Ordinal) < 0
			? value
			: value.Replace("%3A", ":", StringComparison.Ordinal)
				.Replace("%25", "%", StringComparison.Ordinal);
	}

	/// <summary>
	/// Composes two terms into one key no other pair of terms can produce.
	/// </summary>
	/// <param name="first">The first term.</param>
	/// <param name="second">The second term.</param>
	/// <returns>The composed key.</returns>
	public static string Compose(string first, string second)
	{
		ArgumentNullException.ThrowIfNull(first);
		ArgumentNullException.ThrowIfNull(second);

		return string.Concat(Escape(first), SeparatorText, Escape(second));
	}

	/// <summary>
	/// Composes three terms into one key no other triple of terms can produce.
	/// </summary>
	/// <param name="first">The first term.</param>
	/// <param name="second">The second term.</param>
	/// <param name="third">The third term.</param>
	/// <returns>The composed key.</returns>
	public static string Compose(string first, string second, string third)
	{
		ArgumentNullException.ThrowIfNull(first);
		ArgumentNullException.ThrowIfNull(second);
		ArgumentNullException.ThrowIfNull(third);

		return string.Concat(
			Escape(first), SeparatorText, Escape(second), SeparatorText, Escape(third));
	}

	/// <summary>
	/// Composes four or more terms into one key no other sequence of terms can produce.
	/// </summary>
	/// <param name="segments">The terms, in order. At least two are required.</param>
	/// <returns>The composed key.</returns>
	/// <exception cref="System.ArgumentException">
	/// <paramref name="segments" /> holds fewer than two terms, or any term is <see langword="null" />.
	/// </exception>
	public static string Compose(params string[] segments)
	{
		ArgumentNullException.ThrowIfNull(segments);

		if (segments.Length < 2)
		{
			throw new ArgumentException(
				"A composed key needs at least two segments; a single term is already its own key.",
				nameof(segments));
		}

		var encoded = new string[segments.Length];

		for (var i = 0; i < segments.Length; i++)
		{
			encoded[i] = Escape(segments[i] ?? throw new ArgumentNullException(
				nameof(segments),
				$"Segment {i} is null. Every term must be a string; use an empty string for an absent term, "
				+ "which encodes to an empty segment and stays distinguishable from every other value."));
		}

		return string.Join(Separator, encoded);
	}

	/// <summary>
	/// Splits a key produced by <see cref="Compose(string, string)" /> back into its original terms.
	/// </summary>
	/// <param name="key">The composed key.</param>
	/// <param name="count">The exact number of segments the key is expected to carry.</param>
	/// <returns>The decoded terms, in order.</returns>
	/// <remarks>
	/// <para>
	/// <b>Empty segments are preserved, and that is the point of this method existing.</b> The obvious form,
	/// <c>key.Split(':', count, StringSplitOptions.RemoveEmptyEntries)</c>, silently SHIFTS the remaining
	/// segments left when any term is empty, so a key whose user term is empty promotes the tenant into the
	/// user position and addresses a different record entirely. That flag is a defect on its own, wholly
	/// independent of escaping, and it was found in two parsers that had each written it by hand.
	/// </para>
	/// <para>
	/// A key carrying the wrong number of segments is rejected rather than padded or truncated: a caller
	/// that receives fewer terms than it asked for would otherwise read a shifted term as a real one.
	/// </para>
	/// </remarks>
	/// <exception cref="System.ArgumentException">
	/// <paramref name="key" /> does not carry exactly <paramref name="count" /> segments.
	/// </exception>
	public static string[] Split(string key, int count)
	{
		ArgumentNullException.ThrowIfNull(key);
		ArgumentOutOfRangeException.ThrowIfLessThan(count, 2);

		// No RemoveEmptyEntries. See the remarks: dropping an empty segment shifts every later term.
		//
		// And no count limit on the Split either. The limited overload, Split(Separator, count), does not
		// reject a key carrying MORE segments than expected -- it collapses the surplus into the final
		// element, which then still holds a raw separator and decodes to a term that was never written.
		// Splitting completely and comparing the length rejects both directions, and is sound precisely
		// because Escape guarantees no encoded segment can contain the separator.
		var parts = key.Split(Separator);

		if (parts.Length != count)
		{
			throw new ArgumentException(
				$"The key '{key}' carries {parts.Length} segment(s); {count} were expected. A key with the "
				+ "wrong segment count cannot be decoded without guessing which term is missing.",
				nameof(key));
		}

		for (var i = 0; i < parts.Length; i++)
		{
			parts[i] = Unescape(parts[i]);
		}

		return parts;
	}
}
