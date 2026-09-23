// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Mints claim-check identifiers and converts them to storage keys, refusing any identifier the store did
/// not mint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A claim-check reference arrives on the wire and every one of its fields is
/// attacker-influenced. Resolving a storage key from such a field lets whoever can publish to the consumed
/// queue name any object in the configured container and have it fetched with the framework's own
/// credentials, then handed to a handler as the message payload.
/// </para>
/// <para>
/// <b>Why keying on the identifier is not by itself a fix.</b> The in-memory provider is safe while keying
/// on the identifier alone, but that safety comes from its data structure rather than from its discipline:
/// a dictionary lookup cannot escape its container no matter what the key contains. A store that builds a
/// path by interpolation has no such protection, so moving the key derivation from one wire-supplied field
/// to another relocates the defect instead of removing it.
/// </para>
/// <para>
/// <b>The property this type enforces.</b> A wire-supplied identifier cannot influence the STRUCTURE of the
/// storage key. The shape admitted here contains no separator and no traversal sequence, so an identifier
/// that would express one is refused before any key is built. What an attacker retains is the ability to
/// GUESS a well-formed identifier, which is a 128-bit search.
/// </para>
/// <para>
/// <b>The date is carried BY the identifier, not read from the clock.</b> Deriving the partition from
/// <see cref="DateTimeOffset.UtcNow"/> at lookup time means a payload stored at 23:59:59 is looked for under
/// the next day's prefix a second later and reported missing while it is still in the container. Claim check
/// exists for payloads that outlive their message, so crossing midnight is the ordinary case rather than an
/// edge one.
/// </para>
/// </remarks>
internal static partial class ClaimCheckId
{
	/// <summary>
	/// The admitted shape of an identifier once its configured prefix is removed: eight digits of date, a
	/// hyphen, and thirty-two lowercase hex characters.
	/// </summary>
	/// <remarks>
	/// Anchored at both ends deliberately. An unanchored pattern would admit an identifier that merely
	/// CONTAINS a well-formed body, which is the whole attack.
	/// </remarks>
	[GeneratedRegex(@"^\d{8}-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
	private static partial Regex MintedShape();

	/// <summary>
	/// Mints an identifier that carries its own date partition.
	/// </summary>
	/// <param name="prefix">The configured identifier prefix.</param>
	/// <param name="utcNow">The current UTC instant, supplied so callers can keep the value they stamp elsewhere consistent.</param>
	/// <returns>A newly minted claim-check identifier.</returns>
	public static string Create(string prefix, DateTimeOffset utcNow) =>
		string.Concat(
			prefix,
			utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
			"-",
			Guid.NewGuid().ToString("N"));

	/// <summary>
	/// Converts an identifier to its storage key, or refuses it.
	/// </summary>
	/// <param name="prefix">The configured identifier prefix the store mints with.</param>
	/// <param name="keyPrefix">The configured container-relative key prefix, or an empty string.</param>
	/// <param name="claimCheckId">The identifier to resolve, which may have arrived on the wire.</param>
	/// <param name="storageKey">The resolved storage key when this method returns <see langword="true"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the identifier has the shape this store mints; otherwise
	/// <see langword="false"/>, and no key is produced.
	/// </returns>
	public static bool TryGetStorageKey(
		string prefix,
		string keyPrefix,
		string? claimCheckId,
		out string storageKey)
	{
		storageKey = string.Empty;

		if (string.IsNullOrEmpty(claimCheckId)
			|| !claimCheckId.StartsWith(prefix, StringComparison.Ordinal))
		{
			return false;
		}

		var body = claimCheckId[prefix.Length..];
		if (!MintedShape().IsMatch(body))
		{
			return false;
		}

		// The eight leading digits are the partition, and they came from the identifier rather than from
		// the clock. Parsed exactly so a shape such as 20261332 is refused rather than silently forming a
		// key that can never match anything.
		if (!DateTime.TryParseExact(
				body[..8],
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var partition))
		{
			return false;
		}

		storageKey = string.Create(
			CultureInfo.InvariantCulture,
			$"{keyPrefix}{partition:yyyy}/{partition:MM}/{partition:dd}/{claimCheckId}");

		return true;
	}
}
