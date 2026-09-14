// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data;

/// <summary>
/// The framework's single injective encoding for composing a tenant term into a string storage key.
/// </summary>
/// <remarks>
/// <para>
/// Joining terms with a bare separator — <c>"t:" + tenantId + ":" + id</c> — is not injective, because
/// neither term is validated against any charset. A term containing the separator shifts across the
/// boundary and two distinct tuples render one key:
/// </para>
/// <code>
/// tenant "acme"     record "eu:order-42"   ->  t:acme:eu:order-42
/// tenant "acme:eu"  record "order-42"      ->  t:acme:eu:order-42     IDENTICAL
/// </code>
/// <para>
/// Two tenants addressing one key is not a formatting problem: one tenant can read, write and
/// irreversibly erase another's records by passing a single ordinary string.
/// </para>
/// <para>
/// <b>'%' is escaped FIRST, and that is what makes the encoding reversible.</b> Escaping only ':' would
/// map the distinct terms <c>a:b</c> and <c>a%3Ab</c> onto one key — a collision introduced by the
/// escaping itself.
/// </para>
/// <para>
/// <b>The encoding is the IDENTITY on any term containing neither ':' nor '%'</b>, which is what makes
/// it safe to adopt on persisted keys: an ordinary identifier encodes to itself, byte for byte, so no
/// stored record is orphaned. The only keys whose bytes change are the ambiguous ones, which had no
/// single correct owner to begin with.
/// </para>
/// <para>
/// <b>Rejecting ':' is not an option and this is a compatibility constraint, not a preference.</b> A
/// shipped conformance arm exercises tenant <c>a:b</c> as a valid, supported case. Banning the
/// separator would split the framework's own definition of a valid tenant id — the same identifier
/// accepted by the inbox and refused by the event store.
/// </para>
/// <para>
/// This type exists so the encoding has ONE implementation. It was hand-copied into several stores,
/// and an encoding duplicated per store is one edit away from being several encodings.
/// </para>
/// </remarks>
public static class TenantScopedKey
{
	/// <summary>
	/// Marks a key as tenant-scoped. Unchanged from the previous composition, so range probes over the
	/// tenant-scoped key space continue to work.
	/// </summary>
	public const string Prefix = "t:";

	private const char Separator = ':';

	private const char Escape = '%';

	/// <summary>
	/// Encodes one term so that no character in it can be mistaken for a segment boundary.
	/// </summary>
	/// <param name="value">The term to encode.</param>
	/// <returns>
	/// The term with '%' and ':' escaped — identical to <paramref name="value"/> when it contains
	/// neither.
	/// </returns>
	/// <remarks>
	/// Exposed because stores compose different shapes — some join a tenant with one identifier, others
	/// with an aggregate type as well. Each shape stays injective as long as every term goes through
	/// here, and the separator between them is a raw ':'.
	/// </remarks>
	public static string EscapeSegment(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		// ONE PASS, DELIBERATELY. The obvious form is
		//     value.Replace("%", "%25").Replace(":", "%3A")
		// which is correct ONLY in that order, and nothing but a comment enforces it: an edit that
		// swaps the two lines re-introduces the collision this type exists to remove -- "a:b" and
		// "a%3Ab" would both render "a%3Ab" -- and still reads fine. Encoding each character once,
		// here, leaves no ordering for a later edit to get wrong.
		var needed = 0;

		foreach (var c in value)
		{
			if (c is Escape or Separator)
			{
				needed++;
			}
		}

		if (needed == 0)
		{
			// The identity case, and the common one: no allocation, and the bytes a consumer already
			// has stored are reproduced exactly.
			return value;
		}

		return string.Create(value.Length + (needed * 2), value, static (destination, source) =>
		{
			var at = 0;

			foreach (var c in source)
			{
				switch (c)
				{
					case Escape:
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
	/// Reverses <see cref="EscapeSegment"/>.
	/// </summary>
	/// <param name="value">The encoded term.</param>
	/// <returns>The original term.</returns>
	/// <remarks>
	/// ':' is unescaped BEFORE '%', mirroring the encode order. Doing it the other way round would turn
	/// an encoded <c>%253A</c> — a literal "%3A" in the original term — into a separator.
	/// </remarks>
	public static string UnescapeSegment(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return value.Replace("%3A", ":", StringComparison.Ordinal)
			.Replace("%25", "%", StringComparison.Ordinal);
	}

	/// <summary>
	/// Composes the tenant-scoped storage key for <paramref name="id"/> within
	/// <paramref name="tenantId"/>.
	/// </summary>
	/// <param name="tenantId">The owning tenant's term.</param>
	/// <param name="id">The record identifier within that tenant.</param>
	/// <returns>A key no other tenant can produce for any record identifier.</returns>
	public static string Compose(string tenantId, string id)
	{
		ArgumentException.ThrowIfNullOrEmpty(tenantId);
		ArgumentNullException.ThrowIfNull(id);

		return string.Concat(
			Prefix, EscapeSegment(tenantId), stackalloc char[] { Separator }, EscapeSegment(id));
	}

	/// <summary>
	/// Composes a tenant-scoped key from a tenant term and one or more further terms.
	/// </summary>
	/// <param name="tenantId">The owning tenant's term.</param>
	/// <param name="segments">The remaining terms, in order.</param>
	/// <returns>A key no other tenant, and no other term sequence, can produce.</returns>
	public static string Compose(string tenantId, params string[] segments)
	{
		ArgumentException.ThrowIfNullOrEmpty(tenantId);
		ArgumentNullException.ThrowIfNull(segments);

		return Prefix + string.Join(
			Separator,
			segments.Prepend(tenantId).Select(EscapeSegment));
	}
}
