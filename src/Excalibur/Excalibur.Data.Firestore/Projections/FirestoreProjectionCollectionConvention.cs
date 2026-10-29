// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.EventSourcing;

namespace Excalibur.Data.Firestore.Projections;

/// <summary>
/// Resolves the per-projection options identity and the Firestore document segment a projection's
/// subcollection hangs from, so both are derived once rather than re-derived at each call site.
/// </summary>
/// <remarks>
/// Both are keyed on the projection's FULLY-QUALIFIED type name. The simple name is not unique in a
/// solution: two projection classes sharing a name in different namespaces resolved to one options
/// bucket (the second registration overwrote the first's collection name) and to one subcollection,
/// where they read and wrote each other's documents wherever their ids coincided -- with no error and
/// nothing logged.
/// </remarks>
internal static class FirestoreProjectionCollectionConvention
{
	/// <summary>
	/// The maximum length of a Firestore document id, in UTF-8 bytes.
	/// </summary>
	private const int MaxDocumentIdBytes = 1500;

	/// <summary>
	/// The number of hex characters of the fingerprint appended to an over-long segment.
	/// </summary>
	private const int FingerprintChars = 32;

	/// <summary>
	/// The name of the options instance a projection's store settings live under.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>The projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// <see cref="Type.FullName"/> is null only for a type whose identity is not closed -- an open
	/// generic parameter, or a type built on one. A projection type is a concrete closed class, so the
	/// fallback is unreachable through the supported registration; it exists so the convention is total
	/// rather than throwing on a shape nobody can register.
	/// </remarks>
	public static string GetOptionsName<TProjection>()
		where TProjection : class => typeof(TProjection).FullName ?? typeof(TProjection).Name;

	/// <summary>
	/// The Firestore document segment whose "items" subcollection holds a projection's documents.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>A document id that is legal as a Firestore path segment.</returns>
	/// <remarks>
	/// <para>
	/// Derived from the same fully-qualified name as the options identity, then made legal as a document
	/// id. Three constraints apply and each is enforced deterministically rather than assumed: the id may
	/// not contain "/" (it is the path separator, so an unescaped one silently nests the subcollection
	/// somewhere no read looks), it may not be "." or ".." nor match the reserved pattern "__.*__", and it
	/// may be at most 1500 UTF-8 bytes.
	/// </para>
	/// <para>
	/// The transform: the name is escaped for path safety, a reserved shape is prefixed with "type-", and
	/// a name over the byte limit is truncated and suffixed with a hash of the whole name so two long
	/// names sharing a prefix stay distinct. Every step is a pure function of the type name, so one type
	/// always resolves to one segment.
	/// </para>
	/// <para>
	/// THE DECLARED GENERATION PARTICIPATES IN THE SEGMENT, so a change to the read model's shape or
	/// fold addresses a new subcollection rather than mutating the one already serving reads: the new
	/// generation folds from zero while the previous one keeps its documents and its cursor until the
	/// consumer cuts over. A projection declaring no generation, or the first, resolves to byte-exactly
	/// the segment it resolves to without this feature.
	/// </para>
	/// <para>
	/// Applied BEFORE the document-id transform, not after, so the result is always a legal document id.
	/// Qualifying afterwards could push a name already at the 1500-byte limit past it. The hyphen and
	/// the digits the generation contributes pass the transform untouched for every name short enough to
	/// be returned whole: escaping rewrites only "%" and "/", and a name ending in a generation segment
	/// cannot match the reserved "__.*__" shape. For a name long enough to be truncated the literal
	/// segment is cut, but the two generations still resolve apart -- the appended fingerprint is a hash
	/// of the QUALIFIED name, so it differs between them.
	/// </para>
	/// </remarks>
	public static string GetProjectionSegment<TProjection>()
		where TProjection : class => ToDocumentSegment(
			ProjectionVersionConvention.Qualify(GetOptionsName<TProjection>(), typeof(TProjection), '-'));

	/// <summary>
	/// Applies the document-id transform to a fully-qualified type name.
	/// </summary>
	/// <param name="typeName">The fully-qualified type name.</param>
	/// <returns>A legal Firestore document id.</returns>
	internal static string ToDocumentSegment(string typeName)
	{
		// Escapes "/" out of the name, and "%" first so the escaping is itself reversible. A closed
		// generic's full name carries its arguments' assembly-qualified names, which is where a
		// character needing escaping would come from.
		var escaped = FirestoreDocumentId.Escape(typeName);

		// "." and ".." are reserved outright and "__.*__" is reserved for Firestore's own use. A
		// namespace and a type name both legally start or end with an underscore, so the reserved shape
		// is reachable from a real type (namespace "__Legacy", type "Models__") and is prefixed rather
		// than rejected.
		if (escaped is "." or ".."
			|| (escaped.StartsWith("__", StringComparison.Ordinal) && escaped.EndsWith("__", StringComparison.Ordinal)))
		{
			escaped = "type-" + escaped;
		}

		if (Encoding.UTF8.GetByteCount(escaped) <= MaxDocumentIdBytes)
		{
			return escaped;
		}

		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(escaped)))[..FingerprintChars];

		// Truncate by BYTES, not chars, and never mid-character: a split surrogate pair is not valid
		// UTF-8, which is the other thing Firestore refuses.
		var budget = MaxDocumentIdBytes - FingerprintChars - 1;
		var kept = escaped;
		while (Encoding.UTF8.GetByteCount(kept) > budget)
		{
			kept = kept[..^1];
		}

		if (kept.Length > 0 && char.IsHighSurrogate(kept[^1]))
		{
			kept = kept[..^1];
		}

		return kept + "-" + fingerprint;
	}
}
