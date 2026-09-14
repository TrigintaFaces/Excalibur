// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Composes the colon-joined identifiers used to address a grant, so that one tuple maps to one key.
/// </summary>
/// <remarks>
/// <para>
/// A grant is addressed by joining a tenant, a grant type and a qualifier with ':'. None of those
/// restricts the separator, so a bare join is ambiguous: a tenant of <c>a:b</c> with type <c>t</c>
/// produces the same key as a tenant of <c>a</c> with type <c>b</c> shifted one segment along. Because
/// the tenant is one of the joined values, that ambiguity is a cross-tenant authorization question — one
/// tenant's grant can answer for another's — rather than a formatting inconvenience.
/// </para>
/// <para>
/// Escaping each segment before the join makes the mapping one-to-one. The scheme matches the one used
/// for inbox document identifiers: percent-escape the escape character first, then the separator. Order
/// matters in both directions — escaping '%' after ':' would double-escape the marker just written, and
/// restoring '%' before ':' would turn a literal <c>%253A</c> into a separator that was never there.
/// </para>
/// <para>
/// This lives beside the grant contract rather than in any one store because every store that addresses
/// a grant must produce the identical key. Four of them previously composed it by hand, which is how the
/// implementations came to disagree.
/// </para>
/// </remarks>
public static class GrantKeyFormat
{
	private const string EscapedPercent = "%25";
	private const string EscapedColon = "%3A";

	/// <summary>
	/// Composes the scope portion of a grant key from its three segments.
	/// </summary>
	/// <param name="tenantId"> The tenant the grant belongs to. </param>
	/// <param name="grantType"> The grant type. </param>
	/// <param name="qualifier"> The qualifier. </param>
	/// <returns> A key in which each segment is escaped, so no two tuples can produce one key. </returns>
	public static string ComposeScope(string tenantId, string grantType, string qualifier) =>
		$"{Escape(tenantId)}:{Escape(grantType)}:{Escape(qualifier)}";

	/// <summary>
	/// Escapes a single segment so it can neither introduce nor conceal a separator.
	/// </summary>
	/// <param name="value"> The segment to escape. </param>
	/// <returns> The escaped segment, or the original instance when nothing needed escaping. </returns>
	public static string Escape(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		// The common case allocates nothing: most identifiers carry neither character, and this runs on
		// the authorization path where a grant key is composed per check.
		return value.AsSpan().IndexOfAny('%', ':') < 0
			? value
			: value.Replace("%", EscapedPercent, StringComparison.Ordinal)
				.Replace(":", EscapedColon, StringComparison.Ordinal);
	}

	/// <summary>
	/// Restores a segment produced by <see cref="Escape" />.
	/// </summary>
	/// <param name="value"> The escaped segment. </param>
	/// <returns> The original segment, or the same instance when nothing was escaped. </returns>
	public static string Unescape(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return value.IndexOf('%', StringComparison.Ordinal) < 0
			? value
			: value.Replace(EscapedColon, ":", StringComparison.Ordinal)
				.Replace(EscapedPercent, "%", StringComparison.Ordinal);
	}
}
