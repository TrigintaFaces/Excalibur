// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

using EventUpcasting.Events;

using Excalibur.Dispatch.Abstractions;

namespace EventUpcasting.Upgraders;

// ============================================================
// Event Upgraders (V1 -> V2 -> V3)
// ============================================================
// These upgraders transform old event versions to new ones.
// The EventVersionManager finds optimal upgrade paths automatically.
//
// Upgrade chain:
// UserCreatedV1 -> UserCreatedV2 -> UserCreatedV3
// ============================================================

#region V1 to V2 Upgraders

/// <summary>
/// Upgrades UserCreated from V1 to V2.
/// </summary>
/// <remarks>
/// Migration: V1 had no address, V2 adds nullable address.
/// Strategy: Set address to null (unknown).
/// </remarks>
public sealed partial class UserCreatedV1ToV2Upgrader : IMessageUpcaster<UserCreatedV1, UserCreatedV2>
{
	public int FromVersion => 1;
	public int ToVersion => 2;

	public UserCreatedV2 Upcast(UserCreatedV1 oldMessage)
	{
		return new UserCreatedV2(
			oldMessage.AggregateId,
			oldMessage.Version, // aggregate version
			oldMessage.Name,
			oldMessage.Email,
			Address: null);  // V1 had no address
	}
}

/// <summary>
/// Upgrades UserEmailChanged from V1 to V2.
/// </summary>
/// <remarks>
/// No structural changes, just version bump.
/// Note: Email changed events are the same structure V1->V2.
/// </remarks>
public sealed class UserEmailChangedV1ToV2Upgrader : IMessageUpcaster<UserEmailChangedV1, UserCreatedV2>
{
	public int FromVersion => 1;
	public int ToVersion => 2;

	public UserCreatedV2 Upcast(UserEmailChangedV1 oldMessage)
	{
		// Note: This demonstrates that sometimes event types change
		// In a real system, you might keep email changed as a separate event type
		// This is simplified for demonstration purposes
		return new UserCreatedV2(
			oldMessage.AggregateId,
			oldMessage.Version,
			Name: "Unknown", // We don't have the name in email changed events
			oldMessage.NewEmail,
			Address: null);
	}
}

#endregion

#region V2 to V3 Upgraders

/// <summary>
/// Upgrades UserCreated from V2 to V3.
/// </summary>
/// <remarks>
/// Migration: V2 had single Address string, V3 has structured address.
/// Strategy: Parse address string into components using simple heuristics.
/// </remarks>
public sealed partial class UserCreatedV2ToV3Upgrader : IMessageUpcaster<UserCreatedV2, UserCreatedV3>
{
	public int FromVersion => 2;
	public int ToVersion => 3;

	public UserCreatedV3 Upcast(UserCreatedV2 oldMessage)
	{
		var (street, city, postalCode, country) = ParseAddress(oldMessage.Address);

		return new UserCreatedV3(
			oldMessage.AggregateId,
			oldMessage.Version,
			oldMessage.Name,
			oldMessage.Email,
			Street: street,
			City: city,
			PostalCode: postalCode,
			Country: country);
	}

	/// <summary>
	/// Parses a freeform address string into components.
	/// </summary>
	/// <remarks>
	/// Real-world address parsing is complex. This is a simplified version
	/// demonstrating the concept. In production, you might:
	/// - Use a geocoding service
	/// - Use ML-based address parsing
	/// - Mark addresses as "needs review" for manual cleanup
	/// </remarks>
	private static (string? Street, string? City, string? PostalCode, string? Country) ParseAddress(string? address)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			return (null, null, null, null);
		}

		// Simple heuristic: split by comma
		// Example: "123 Main St, Springfield, IL 62701, USA"
		var parts = address.Split(',', StringSplitOptions.TrimEntries);

		return parts.Length switch
		{
			0 => (null, null, null, null),
			1 => (parts[0], null, null, null),
			2 => (parts[0], parts[1], null, null),
			3 => ParseThreeParts(parts),
			_ => (parts[0], parts[1], ExtractPostalCode(parts[2]), parts[^1])
		};
	}

	private static (string? Street, string? City, string? PostalCode, string? Country) ParseThreeParts(string[] parts)
	{
		// Could be "Street, City, Country" or "Street, City State ZIP"
		var postalCode = ExtractPostalCode(parts[2]);
		if (postalCode != null)
		{
			return (parts[0], parts[1], postalCode, null);
		}

		return (parts[0], parts[1], null, parts[2]);
	}

	private static string? ExtractPostalCode(string part)
	{
		// Look for common postal code patterns
		var match = PostalCodePattern().Match(part);
		return match.Success ? match.Value : null;
	}

	[GeneratedRegex(@"\b\d{5}(-\d{4})?\b|\b[A-Z]\d[A-Z]\s?\d[A-Z]\d\b", RegexOptions.IgnoreCase)]
	private static partial Regex PostalCodePattern();
}

/// <summary>
/// Upgrades UserAddressChanged from V2 to V3.
/// </summary>
public sealed partial class UserAddressChangedV2ToV3Upgrader : IMessageUpcaster<UserAddressChangedV2, UserAddressChangedV3>
{
	public int FromVersion => 2;
	public int ToVersion => 3;

	public UserAddressChangedV3 Upcast(UserAddressChangedV2 oldMessage)
	{
		var (oldStreet, oldCity, oldPostalCode, oldCountry) = ParseAddress(oldMessage.OldAddress);
		var (newStreet, newCity, newPostalCode, newCountry) = ParseAddress(oldMessage.NewAddress);

		return new UserAddressChangedV3(
			oldMessage.AggregateId,
			oldMessage.Version,
			OldStreet: oldStreet,
			OldCity: oldCity,
			OldPostalCode: oldPostalCode,
			OldCountry: oldCountry,
			NewStreet: newStreet,
			NewCity: newCity,
			NewPostalCode: newPostalCode,
			NewCountry: newCountry);
	}

	private static (string? Street, string? City, string? PostalCode, string? Country) ParseAddress(string? address)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			return (null, null, null, null);
		}

		var parts = address.Split(',', StringSplitOptions.TrimEntries);

		return parts.Length switch
		{
			0 => (null, null, null, null),
			1 => (parts[0], null, null, null),
			2 => (parts[0], parts[1], null, null),
			3 => ParseThreeParts(parts),
			_ => (parts[0], parts[1], ExtractPostalCode(parts[2]), parts[^1])
		};
	}

	private static (string? Street, string? City, string? PostalCode, string? Country) ParseThreeParts(string[] parts)
	{
		var postalCode = ExtractPostalCode(parts[2]);
		if (postalCode != null)
		{
			return (parts[0], parts[1], postalCode, null);
		}

		return (parts[0], parts[1], null, parts[2]);
	}

	private static string? ExtractPostalCode(string part)
	{
		var match = PostalCodePattern().Match(part);
		return match.Success ? match.Value : null;
	}

	[GeneratedRegex(@"\b\d{5}(-\d{4})?\b|\b[A-Z]\d[A-Z]\s?\d[A-Z]\d\b", RegexOptions.IgnoreCase)]
	private static partial Regex PostalCodePattern();
}

#endregion

#region Direct V1 to V3 Upgrader (Skip V2)

/// <summary>
/// Direct upgrade from V1 to V3 (skipping V2).
/// </summary>
/// <remarks>
/// The EventVersionManager uses BFS to find the shortest path.
/// This upgrader provides a direct V1->V3 path, avoiding the V1->V2->V3 chain.
/// Benefits:
/// - Fewer transformations (1 instead of 2)
/// - Better performance for old events
/// - Opportunity for optimized migration logic
/// </remarks>
public sealed class UserCreatedV1ToV3DirectUpgrader : IMessageUpcaster<UserCreatedV1, UserCreatedV3>
{
	public int FromVersion => 1;
	public int ToVersion => 3;

	public UserCreatedV3 Upcast(UserCreatedV1 oldMessage)
	{
		// Direct transformation - no address data in V1
		return new UserCreatedV3(
			oldMessage.AggregateId,
			oldMessage.Version,
			oldMessage.Name,
			oldMessage.Email,
			Street: null,
			City: null,
			PostalCode: null,
			Country: null);
	}
}

#endregion
