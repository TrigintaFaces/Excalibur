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
	/// For a caller that must degrade rather than fail when a message is unnamed. A stored identity is
	/// not such a caller -- use <see cref="GetName(Type)"/> there.
	/// </remarks>
	public static string? GetDeclaredName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		return DeclaredNames.GetOrAdd(type, static t =>
			t.GetCustomAttribute<MessageNameAttribute>(inherit: false)?.Name);
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
