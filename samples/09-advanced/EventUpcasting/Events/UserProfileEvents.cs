// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace EventUpcasting.Events;

// ============================================================
// Event Schema Evolution Example
// ============================================================
// This file demonstrates how event schemas evolve over time.
// We keep ALL versions to show the transformation chain.
//
// Evolution timeline:
// V1: Initial schema - simple name, email
// V2: Added address (single string)
// V3: Split address into components (street, city, postal code, country)
//
// NOTE: IVersionedMessage.Version (schema version, int) is different from
// IDomainEvent.Version (aggregate version, long). We implement the schema
// version explicitly to avoid conflicts.
// ============================================================

#region Version 1 Events

/// <summary>
/// V1: Original user created event (legacy).
/// </summary>
/// <remarks>
/// This was the original event format with basic user info.
/// Still exists in the event store from older users.
/// </remarks>
public sealed record UserCreatedV1(
	string AggregateId,
	long AggregateVersion,
	string Name,
	string Email) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 1;
	string IVersionedMessage.MessageType => "UserCreated";
}

/// <summary>
/// V1: Original email changed event.
/// </summary>
public sealed record UserEmailChangedV1(
	string AggregateId,
	long AggregateVersion,
	string OldEmail,
	string NewEmail) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 1;
	string IVersionedMessage.MessageType => "UserEmailChanged";
}

#endregion

#region Version 2 Events

/// <summary>
/// V2: User created with address field added.
/// </summary>
/// <remarks>
/// Business requirement: We need to store user addresses.
/// Decision: Add a single "Address" string field.
/// </remarks>
public sealed record UserCreatedV2(
	string AggregateId,
	long AggregateVersion,
	string Name,
	string Email,
	string? Address) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 2;
	string IVersionedMessage.MessageType => "UserCreated";
}

/// <summary>
/// V2: User address changed event.
/// </summary>
public sealed record UserAddressChangedV2(
	string AggregateId,
	long AggregateVersion,
	string? OldAddress,
	string NewAddress) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 2;
	string IVersionedMessage.MessageType => "UserAddressChanged";
}

#endregion

#region Version 3 Events (Current)

/// <summary>
/// V3: User created with structured address (current version).
/// </summary>
/// <remarks>
/// Business requirement: International shipping needs structured addresses.
/// Decision: Split Address into Street, City, PostalCode, Country.
/// </remarks>
public sealed record UserCreatedV3(
	string AggregateId,
	long AggregateVersion,
	string Name,
	string Email,
	string? Street,
	string? City,
	string? PostalCode,
	string? Country) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 3;
	string IVersionedMessage.MessageType => "UserCreated";
}

/// <summary>
/// V3: User address changed with structured address (current version).
/// </summary>
public sealed record UserAddressChangedV3(
	string AggregateId,
	long AggregateVersion,
	string? OldStreet,
	string? OldCity,
	string? OldPostalCode,
	string? OldCountry,
	string? NewStreet,
	string? NewCity,
	string? NewPostalCode,
	string? NewCountry) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 3;
	string IVersionedMessage.MessageType => "UserAddressChanged";
}

/// <summary>
/// V3: User name changed event (unchanged from V1, but explicitly versioned).
/// </summary>
public sealed record UserNameChangedV3(
	string AggregateId,
	long AggregateVersion,
	string OldName,
	string NewName) : DomainEvent(AggregateId, AggregateVersion), IVersionedMessage
{
	int IVersionedMessage.Version => 3;
	string IVersionedMessage.MessageType => "UserNameChanged";
}

#endregion
