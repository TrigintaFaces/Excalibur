// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Excalibur.EventSourcing;

/// <summary>
/// Computes and verifies the integrity checksum recorded for an applied migration script.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every <see cref="IMigrator"/> implementation so the recording side and the verifying
/// side cannot disagree. Two migrators that each carried their own copy of this would eventually
/// hash the same script to two different values, and the failure mode of that is a provider
/// refusing to start over a difference that does not exist.
/// </para>
/// <para>
/// LINE ENDINGS ARE NORMALIZED BEFORE HASHING, AND NOTHING ELSE IS. A repository that declares its
/// sources <c>text=auto</c> hands out CRLF on Windows and LF elsewhere for byte-identical committed
/// content, so the same migration embedded from the same commit produces two different resources
/// depending on the machine that built the package. Hashing the raw bytes would make a routine
/// cross-platform upgrade indistinguishable from an edited migration, and the consequence of that
/// confusion is a service that refuses to start. A CR/LF translation cannot express a schema change,
/// so folding it away cannot mask one.
/// </para>
/// <para>
/// Nothing further is normalized, deliberately. Trailing whitespace looks equally cosmetic and is
/// not: it is inside a multi-line string literal as surely as it is at the end of a statement, and a
/// comparison that trimmed it would silently accept an edit to a literal value. A byte order mark
/// needs no handling here because <see cref="StreamReader"/> consumes it during decoding, so it
/// never reaches this method.
/// </para>
/// </remarks>
internal static class MigrationScriptChecksum
{
	/// <summary>
	/// Computes the checksum to record for a migration script.
	/// </summary>
	/// <param name="scriptContent">The decoded script text.</param>
	/// <returns>An uppercase hexadecimal SHA-256 of the line-ending-normalized script.</returns>
	internal static string Compute(string scriptContent)
	{
		ArgumentNullException.ThrowIfNull(scriptContent);

		return Hash(NormalizeLineEndings(scriptContent));
	}

	/// <summary>
	/// Determines whether a checksum recorded for an applied migration still describes the script.
	/// </summary>
	/// <param name="storedChecksum">The checksum read back from the migration history table.</param>
	/// <param name="scriptContent">The decoded text of the script carrying that migration's id today.</param>
	/// <returns><see langword="true"/> when the script is unchanged; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// The CRLF form is accepted as well as the canonical LF one. Rows written before checksums were
	/// normalized hold a hash of the raw resource, which on a Windows-built package is the CRLF
	/// rendering of the same content. Accepting it costs nothing in strictness — it can only match
	/// when the script's content is identical to what was applied, which is the definition of not
	/// having drifted — and refusing it would fail every database that has ever been migrated by a
	/// released version of this package.
	/// </remarks>
	internal static bool Matches(string storedChecksum, string scriptContent)
	{
		ArgumentNullException.ThrowIfNull(storedChecksum);
		ArgumentNullException.ThrowIfNull(scriptContent);

		var normalized = NormalizeLineEndings(scriptContent);

		return string.Equals(storedChecksum, Hash(normalized), StringComparison.OrdinalIgnoreCase)
			|| string.Equals(storedChecksum, Hash(ToCrLf(normalized)), StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Builds the message a migrator refuses with when applied scripts no longer match their record.
	/// </summary>
	/// <param name="migrationIds">The ids of the migrations whose bodies no longer match.</param>
	/// <returns>An operator-facing message naming the affected migrations and the two ways out.</returns>
	internal static string DescribeDrift(IReadOnlyCollection<string> migrationIds)
	{
		ArgumentNullException.ThrowIfNull(migrationIds);

		return string.Format(
			CultureInfo.InvariantCulture,
			"Refusing to migrate. {0} migration(s) recorded as applied no longer match the script now " +
			"carrying that id in the migration assembly: {1}. A numbered migration's body is fixed once " +
			"it has been applied — this database ran a different script than the one present today, so it " +
			"is not in the state that script describes, and applying later migrations on top of it is " +
			"unsafe. Either restore the recorded migration's original body and ship the correction as a " +
			"NEW numbered migration, or — only if this database is known to already carry the current " +
			"body's effect — roll back past the listed migration(s) and re-apply. No migrations were run.",
			migrationIds.Count,
			string.Join(", ", migrationIds));
	}

	private static string NormalizeLineEndings(string content) =>
		content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

	private static string ToCrLf(string lfNormalized) =>
		lfNormalized.Replace("\n", "\r\n", StringComparison.Ordinal);

	private static string Hash(string content) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
