// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Excalibur.Application.Requests;

/// <summary>
/// Provides convention-based resolution of activity names, display names, and descriptions.
/// Reads an optional <see cref="ActivityAttribute"/> and falls back to type-name humanization.
/// </summary>
internal static partial class ActivityNameConvention
{
	private const int MaxCacheEntries = 1024;

	private static readonly ConcurrentDictionary<Type, (string Name, string DisplayName, string Description)> Cache = new();

	private static readonly string[] KnownSuffixes =
	[
		"Notification",
		"Command",
		"Query",
		"Job",
	];

	/// <summary>
	/// Resolves the machine-readable activity name for the specified type.
	/// Format: <c>{Namespace}:{TypeName}</c> (e.g., <c>MyApp.Orders:PlaceOrderCommand</c>).
	/// </summary>
	internal static string ResolveName(Type type)
	{
		var (name, _, _) = Resolve(type);
		return name;
	}

	/// <summary>
	/// Resolves the display name for the specified activity type.
	/// </summary>
	internal static string ResolveDisplayName(Type type)
	{
		var (_, displayName, _) = Resolve(type);
		return displayName;
	}

	/// <summary>
	/// Resolves the description for the specified activity type.
	/// </summary>
	internal static string ResolveDescription(Type type)
	{
		var (_, _, description) = Resolve(type);
		return description;
	}

	private static (string Name, string DisplayName, string Description) Resolve(Type type)
	{
		if (Cache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		var result = ComputeFromConvention(type);

		if (Cache.Count < MaxCacheEntries)
		{
			Cache.TryAdd(type, result);
		}

		return result;
	}

	private static (string Name, string DisplayName, string Description) ComputeFromConvention(Type type)
	{
		var ns = type.Namespace ?? type.Name;
		var name = $"{ns}:{type.Name}";

		var attr = Attribute.GetCustomAttribute(type, typeof(ActivityAttribute)) as ActivityAttribute;

		string displayName;
		string description;

		if (attr is not null)
		{
			displayName = attr.DisplayName;
			description = attr.Description ?? BuildQualifiedName(type, displayName);
		}
		else
		{
			var humanized = HumanizeTypeName(type);
			var qualified = BuildQualifiedName(type, humanized);
			displayName = qualified;
			description = qualified;
		}

		return (name, displayName, description);
	}

	/// <summary>
	/// Humanizes a type name by stripping known suffixes and splitting PascalCase.
	/// </summary>
	/// <example>
	/// PlaceOrderCommand => "Place Order"
	/// GetOrderSummaryQuery => "Get Order Summary"
	/// ProcessOrderBatchJob => "Process Order Batch"
	/// OrderShippedNotification => "Order Shipped"
	/// </example>
	internal static string HumanizeTypeName(Type type)
	{
		var name = type.Name;

		// Handle generic types: strip `1 suffix
		var backtickIndex = name.IndexOf('`', StringComparison.Ordinal);
		if (backtickIndex >= 0)
		{
			name = name[..backtickIndex];
		}

		// Strip known activity suffixes
		foreach (var suffix in KnownSuffixes)
		{
			if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
			{
				name = name[..^suffix.Length];
				break;
			}
		}

		return PascalCaseSplitRegex().Replace(name, " $1$2").Trim();
	}

	private static string BuildQualifiedName(Type type, string humanizedName)
	{
		var ns = type.Namespace ?? type.Name;
		return $"{ns}: {humanizedName}";
	}

	[GeneratedRegex(@"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])")]
	private static partial Regex PascalCaseSplitRegex();
}
