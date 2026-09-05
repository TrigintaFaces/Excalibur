// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Excalibur.Dispatch;

/// <summary>
/// Reads the stable name a message declares through <see cref="MessageNameAttribute"/>.
/// </summary>
/// <remarks>
/// A message's stored identity is declared, never derived. Nothing here falls back to a name taken
/// from the type, because such a name embeds the namespace, assembly and assembly version -- so moving
/// the type, or shipping a new version, would change an identity the consumer never chose and make
/// everything already written unreadable.
/// </remarks>
public static class MessageNameHelper
{
	private static readonly ConcurrentDictionary<Type, string?> DeclaredNames = new();
	private static readonly ConcurrentDictionary<Type, string[]> DeclaredAliases = new();

	/// <summary>
	/// Gets the stable name <paramref name="type"/> declares.
	/// </summary>
	/// <param name="type">The message type.</param>
	/// <returns>The declared name.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><paramref name="type"/> declares no name.</exception>
	public static string GetName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		return GetDeclaredName(type) ?? throw new InvalidOperationException(
			$"Message type '{type}' declares no [MessageName]. A message's stored identity must be "
			+ "declared rather than derived from the type, because a derived name changes when the "
			+ "type's namespace, assembly or assembly version changes and everything already stored "
			+ $"under the old one becomes unreadable. Add [MessageName(\"...\")] to '{type.Name}'. "
			+ "Choose the name once: it is permanent, and renaming the type later is done by keeping "
			+ "the old name with [MessageNameAlias].");
	}

	/// <summary>
	/// Gets the stable name <paramref name="type"/> declares, or <see langword="null"/> if it declares
	/// none.
	/// </summary>
	/// <param name="type">The message type.</param>
	/// <returns>The declared name, or <see langword="null"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// <para>
	/// For a caller that must degrade rather than fail when a message is unnamed. A stored identity is
	/// not such a caller -- use <see cref="GetName(Type)"/> there.
	/// </para>
	/// <para>
	/// A closed generic composes its name from the name its open definition declares, so one declaration
	/// covers every construction and two constructions never claim one name. The composition follows the
	/// rule the data contract serializer has used for generic types since .NET 3.0 -- the declared name,
	/// the word <c>Of</c>, then the declared name of each type argument, falling back to the argument's
	/// type name where it declares none. <c>Drawing&lt;Square, RedBrush&gt;</c> is
	/// <c>DrawingOfSquareRedBrush</c> there and here alike.
	/// </para>
	/// <para>
	/// The hash of argument namespaces that the data contract serializer appends is deliberately not
	/// reproduced: it exists to separate two arguments sharing a name in different namespaces, which
	/// here is refused loudly at registration rather than accepted silently, and it would make every
	/// generic message name unreadable to buy that.
	/// </para>
	/// </remarks>
	public static string? GetDeclaredName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		return DeclaredNames.GetOrAdd(type, static t =>
		{
			// An attribute on the open definition is returned for every construction of it, so without
			// the composition below every construction would answer with one shared name -- and a name
			// two types claim is worse than no name at all: it resolves to neither.
			var declared = t.GetCustomAttribute<MessageNameAttribute>(inherit: false)?.Name;

			return declared is not null && t.IsConstructedGenericType
				? declared + "Of" + string.Concat(
					t.GetGenericArguments().Select(static a => GetDeclaredName(a) ?? a.Name))
				: declared;
		});
	}

	/// <summary>
	/// Gets the names <paramref name="type"/> declares it was previously known by.
	/// </summary>
	/// <param name="type">The message type.</param>
	/// <returns>The declared aliases; empty if it declares none.</returns>
	internal static IReadOnlyList<string> GetDeclaredAliases(Type type) =>
		DeclaredAliases.GetOrAdd(type, static t =>
			t.GetCustomAttributes<MessageNameAliasAttribute>(inherit: false)
				.Select(static a => a.Name)
				.ToArray());
}
