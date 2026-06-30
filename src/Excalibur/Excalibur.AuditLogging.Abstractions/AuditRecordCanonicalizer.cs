// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers.Binary;
using System.Text;

namespace Excalibur.AuditLogging;

/// <summary>
/// Produces the deterministic, unambiguous canonical byte representation of an audit record's
/// integrity-covered fields, for use as the input to <see cref="IAuditIntegrityStrategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// The keyed MAC protects the <em>bytes</em> it is computed over, not the field structure — so the
/// canonical form must be both <b>deterministic</b> (the same fields always produce byte-identical output)
/// and <b>injective on field boundaries</b> (distinct field sequences can never collide to the same bytes).
/// This helper guarantees both:
/// </para>
/// <list type="bullet">
/// <item><description>A leading <b>version</b> byte (<see cref="CanonicalVersion"/>) so the canonical form
/// can evolve while older records stay verifiable under their original version.</description></item>
/// <item><description><b>Length-prefixed</b> fields (a present/absent marker plus a big-endian length),
/// never naive concatenation — so <c>["a","bc"]</c> and <c>["ab","c"]</c> produce different bytes, and a
/// <see langword="null"/> field is distinct from an empty one.</description></item>
/// <item><description>Fixed <b>UTF-8</b> encoding (culture-invariant) — callers must supply
/// culture-invariant field strings in a stable order.</description></item>
/// </list>
/// <para>
/// Verification must re-canonicalize the live reloaded fields through this same helper (never persist and
/// re-MAC a stored canonical blob), so the integrity check covers the queryable record.
/// </para>
/// </remarks>
public static class AuditRecordCanonicalizer
{
	/// <summary>The current canonical-format version, written as the first byte of every canonical buffer.</summary>
	public const byte CanonicalVersion = 1;

	private const byte FieldAbsent = 0x00;
	private const byte FieldPresent = 0x01;

	/// <summary>
	/// Canonicalizes an ordered set of integrity-covered fields into deterministic, unambiguous bytes.
	/// </summary>
	/// <param name="fields">
	/// The record's integrity-covered fields, in a stable order, each already rendered as a
	/// culture-invariant string (or <see langword="null"/> for an absent field).
	/// </param>
	/// <returns>The canonical byte representation, prefixed with <see cref="CanonicalVersion"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is <see langword="null"/>.</exception>
	public static byte[] Canonicalize(params string?[] fields)
	{
		ArgumentNullException.ThrowIfNull(fields);

		// version(1) + per field: marker(1) [+ length(4) + utf8 bytes]
		var capacity = 1;
		foreach (var field in fields)
		{
			capacity += 1;
			if (field is not null)
			{
				capacity += 4 + Encoding.UTF8.GetByteCount(field);
			}
		}

		var buffer = new byte[capacity];
		var offset = 0;
		buffer[offset++] = CanonicalVersion;

		foreach (var field in fields)
		{
			if (field is null)
			{
				buffer[offset++] = FieldAbsent;
				continue;
			}

			buffer[offset++] = FieldPresent;
			var written = Encoding.UTF8.GetBytes(field, 0, field.Length, buffer, offset + 4);
			BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset, 4), written);
			offset += 4 + written;
		}

		return buffer;
	}
}
