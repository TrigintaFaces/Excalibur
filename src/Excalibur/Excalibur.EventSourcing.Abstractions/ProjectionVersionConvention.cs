// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Excalibur.EventSourcing;

/// <summary>
/// Applies a projection's declared generation to the destination name its provider resolved.
/// </summary>
/// <remarks>
/// <para>
/// Every provider that addresses a per-projection destination composes its name through this one
/// function, so the rule for where a generation appears -- and, more importantly, the rule for when it
/// does not appear at all -- exists in a single place. A provider that re-derived it would be free to
/// disagree, and the disagreement would show up as a read model that folded from zero on an upgrade
/// nobody meant to be a cutover.
/// </para>
/// <para>
/// Providers call this at the point where they have already resolved the destination name they would
/// have used otherwise -- after any configured name override and any prefix -- and pass the separator
/// their own identifiers permit.
/// </para>
/// </remarks>
public static class ProjectionVersionConvention
{
	/// <summary>
	/// The generation a projection has when it declares none. It resolves to an unchanged destination name.
	/// </summary>
	private const int FirstGeneration = 1;

	/// <summary>
	/// Applies <paramref name="projectionType"/>'s declared generation to a resolved destination name.
	/// </summary>
	/// <param name="destinationName">
	/// The destination name the provider resolved without regard to the generation -- the index,
	/// container, collection or table it would address for generation 1.
	/// </param>
	/// <param name="projectionType">The read-model type whose declared generation is read.</param>
	/// <param name="separator">
	/// The character joining the generation segment to the name. Pass the one the provider's own
	/// identifiers allow: a hyphen for a search index, an underscore for a SQL identifier.
	/// </param>
	/// <returns>
	/// <paramref name="destinationName"/> unchanged for generation 1 or for a type declaring no
	/// generation; otherwise that name followed by the separator and the generation, such as
	/// <c>orders-v2</c>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="destinationName"/> is blank.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="projectionType"/> is null.</exception>
	public static string Qualify(string destinationName, Type projectionType, char separator)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
		ArgumentNullException.ThrowIfNull(projectionType);

		var version = projectionType.GetCustomAttribute<ProjectionVersionAttribute>()?.Version ?? FirstGeneration;

		// THE UNVERSIONED NAME IS RETURNED UNTOUCHED, and that is the whole safety property of this
		// function rather than a convenience. Every read model in existence declares no generation, so a
		// rule that appended a segment unconditionally would rename every destination the framework
		// addresses -- each one folding from zero, each one losing the cursor of a read model that was
		// serving correctly. Appending only above the first generation is the one shape in which that
		// outcome is not expressible by configuration: reaching it requires editing this comparison.
		return version <= FirstGeneration
			? destinationName
			: string.Concat(destinationName, separator.ToString(), "v", version.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}
}
