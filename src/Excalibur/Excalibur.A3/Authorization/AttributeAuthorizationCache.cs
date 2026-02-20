// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Caches <see cref="RequirePermissionAttribute"/> lookup results per message type for performance.
/// </summary>
/// <remarks>
/// <para>
/// This cache ensures that attribute scanning via reflection only happens once per message type,
/// avoiding the performance overhead of repeated reflection calls during message processing.
/// </para>
/// <para>
/// The cache is thread-safe and uses <see cref="ConcurrentDictionary{TKey, TValue}"/> internally.
/// </para>
/// </remarks>
public sealed class AttributeAuthorizationCache
{
	private readonly ConcurrentDictionary<Type, RequirePermissionAttribute[]> _attributeCache = new();
	private readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propertyCache = new();

	/// <summary>
	/// Gets the cached <see cref="RequirePermissionAttribute"/> attributes for a message type.
	/// </summary>
	/// <param name="messageType">The message type to get attributes for.</param>
	/// <returns>An array of <see cref="RequirePermissionAttribute"/> instances, or an empty array if none are defined.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="messageType"/> is null.</exception>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Attribute lookup uses reflection which may not work correctly with trimming.")]
	public RequirePermissionAttribute[] GetAttributes(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		return _attributeCache.GetOrAdd(messageType, static type =>
			[.. type.GetCustomAttributes<RequirePermissionAttribute>(inherit: true)]);
	}

	/// <summary>
	/// Checks if the message type has any <see cref="RequirePermissionAttribute"/> attributes.
	/// </summary>
	/// <param name="messageType">The message type to check.</param>
	/// <returns><see langword="true"/> if the type has one or more attributes; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="messageType"/> is null.</exception>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Attribute lookup uses reflection which may not work correctly with trimming.")]
	public bool HasAttributes(Type messageType) => GetAttributes(messageType).Length > 0;

	/// <summary>
	/// Extracts the resource ID value from a message using the specified property name.
	/// </summary>
	/// <param name="message">The message to extract the resource ID from.</param>
	/// <param name="propertyName">The name of the property containing the resource ID.</param>
	/// <returns>The resource ID as a string, or <see langword="null"/> if the property doesn't exist or the value is null.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Property lookup uses reflection which may not work correctly with trimming.")]
	public string? ExtractResourceId(object message, string? propertyName)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (string.IsNullOrWhiteSpace(propertyName))
		{
			return null;
		}

		var messageType = message.GetType();
		var cacheKey = (messageType, propertyName);

		var property = _propertyCache.GetOrAdd(cacheKey, key =>
			key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance));

		if (property is null)
		{
			return null;
		}

		var value = property.GetValue(message);
		return value?.ToString();
	}
}
