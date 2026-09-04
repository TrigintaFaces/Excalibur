// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Dispatch;

/// <summary>
/// Enforces the shape of a declared message name.
/// </summary>
/// <remarks>
/// A message name is permanent, is written into every stored record, and is shared across every
/// provider the framework supports -- so it is held to a shape that survives all of them rather than
/// described in documentation and hoped for. The accepted set is deliberately narrow: it needs no
/// escaping in a database column, a URL, a file name, a header, or a topic name.
/// </remarks>
internal static partial class MessageNameValidator
{
	/// <summary>The longest a message name may be.</summary>
	/// <remarks>
	/// Chosen to fit comfortably inside the narrowest event-type column any shipped provider declares,
	/// leaving room for a provider that indexes it alongside a tenant.
	/// </remarks>
	internal const int MaxLength = 256;

	/// <summary>
	/// Returns <paramref name="name"/> if it is a usable message name, and throws if it is not.
	/// </summary>
	/// <param name="name">The candidate name.</param>
	/// <returns>The validated name.</returns>
	/// <exception cref="ArgumentException">The name is empty, too long, or has an unusable shape.</exception>
	internal static string Validate(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (name.Length > MaxLength)
		{
			throw new ArgumentException(
				$"The message name '{name}' is {name.Length} characters; the maximum is {MaxLength}. "
				+ "The name is stored on every record, so it has to fit the narrowest column a provider "
				+ "declares for it.",
				nameof(name));
		}

		if (!AcceptedShape().IsMatch(name))
		{
			throw new ArgumentException(
				$"The message name '{name}' cannot be used. A name must start and end with a letter or "
				+ "digit, and may otherwise contain letters, digits, and the separators '.', '-', '_' "
				+ "and ':'. The name is written into database columns, URLs, file names and broker "
				+ "topics, and this is the set that needs no escaping in any of them. "
				+ "'Contoso.Sales.CustomerCreated' is a good name: a publisher prefix, the bounded "
				+ "context, then the event.",
				nameof(name));
		}

		return name;
	}

	[GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9._:-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
	private static partial Regex AcceptedShape();
}
