// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Correlates messages to saga instances using attribute decoration or convention-based
/// property name matching.
/// </summary>
/// <remarks>
/// <para>
/// The correlator uses a two-phase resolution strategy:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Attribute-based:</b> Looks for properties decorated with
/// <see cref="SagaMessageCorrelationAttribute"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Convention-based:</b> Falls back to well-known property names:
/// <c>SagaId</c>, <c>CorrelationId</c>.
/// </description>
/// </item>
/// </list>
/// <para>
/// Property accessor delegates are cached per message type for performance.
/// </para>
/// </remarks>
public sealed class ConventionBasedCorrelator
{
	private static readonly string[] ConventionPropertyNames =
	[
		"SagaId",
		"CorrelationId",
	];

	private readonly ConcurrentDictionary<Type, Func<object, string?>?> _accessorCache = new();

	/// <summary>
	/// Attempts to extract a correlation identifier from a message using attribute
	/// decoration or convention-based property matching.
	/// </summary>
	/// <param name="message">The message to extract the correlation ID from.</param>
	/// <param name="correlationId">
	/// When this method returns, contains the correlation ID if found;
	/// otherwise, <see langword="null"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if a correlation ID was successfully extracted;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public bool TryGetCorrelationId(object message, out string? correlationId)
	{
		ArgumentNullException.ThrowIfNull(message);

		var accessor = _accessorCache.GetOrAdd(
			message.GetType(),
			static type => ResolveAccessor(type));

		if (accessor is not null)
		{
			correlationId = accessor(message);
			return correlationId is not null;
		}

		correlationId = null;
		return false;
	}

	private static Func<object, string?>? ResolveAccessor(Type messageType)
	{
		// Phase 1: Attribute-based resolution
		var attributeProperty = FindAttributeDecoratedProperty(messageType);
		if (attributeProperty is not null)
		{
			return CreateAccessor(attributeProperty);
		}

		// Phase 2: Convention-based resolution
		var conventionProperty = FindConventionProperty(messageType);
		if (conventionProperty is not null)
		{
			return CreateAccessor(conventionProperty);
		}

		return null;
	}

	private static PropertyInfo? FindAttributeDecoratedProperty(Type type)
	{
		return type
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.FirstOrDefault(p =>
				p.GetCustomAttribute<SagaMessageCorrelationAttribute>() is not null &&
				p.PropertyType == typeof(string));
	}

	private static PropertyInfo? FindConventionProperty(Type type)
	{
		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		foreach (var name in ConventionPropertyNames)
		{
			var property = properties.FirstOrDefault(p =>
				string.Equals(p.Name, name, StringComparison.Ordinal) &&
				p.PropertyType == typeof(string));

			if (property is not null)
			{
				return property;
			}
		}

		return null;
	}

	private static Func<object, string?> CreateAccessor(PropertyInfo property)
	{
		return message => (string?)property.GetValue(message);
	}
}
