// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

using Excalibur.Dispatch.Abstractions;
namespace UserProfileVersioning.Events;

#region UserProfileUpdated Event Version History
// This file demonstrates the evolution of a UserProfileUpdatedEvent over time:
//
// V1 (Original): Basic profile with single Name field
//     - Used from initial launch
//     - Name stored as single string (e.g., "John Doe")
//
// V2 (Name Split): Separated into FirstName and LastName
//     - Migration: Split Name on first space
//     - Enables proper sorting and formal addressing
//
// V3 (GDPR Compliance): Added ConsentGiven flag and ConsentDate
//     - Migration: Legacy profiles assume consent (grandfathered)
//     - Enables tracking of GDPR consent for data processing
//
// V4 (Privacy Enhancement): Added Email with encryption indicator
//     - Migration: Email defaults to empty, IsEmailEncrypted defaults to false
//     - Supports field-level encryption for sensitive data
#endregion

/// <summary>
/// V1: Original profile event with single Name field.
/// </summary>
public sealed record UserProfileUpdatedEventV1 : IDispatchMessage, IVersionedMessage
{
	/// <summary>User's unique identifier.</summary>
	public Guid UserId { get; init; }

	/// <summary>User's full name as a single string.</summary>
	public string Name { get; init; } = string.Empty;

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string MessageType => "UserProfileUpdatedEvent";
}

/// <summary>
/// V2: Split name into FirstName and LastName.
/// </summary>
/// <remarks>
/// This version supports:
/// <list type="bullet">
///   <item>Proper alphabetical sorting by last name</item>
///   <item>Formal addressing (Dear Mr./Ms. LastName)</item>
///   <item>Personalized greetings (Hi FirstName!)</item>
/// </list>
/// </remarks>
public sealed record UserProfileUpdatedEventV2 : IDispatchMessage, IVersionedMessage
{
	/// <summary>User's unique identifier.</summary>
	public Guid UserId { get; init; }

	/// <summary>User's first/given name.</summary>
	public string FirstName { get; init; } = string.Empty;

	/// <summary>User's last/family name.</summary>
	public string LastName { get; init; } = string.Empty;

	/// <inheritdoc/>
	public int Version => 2;

	/// <inheritdoc/>
	public string MessageType => "UserProfileUpdatedEvent";
}

/// <summary>
/// V3: Added GDPR consent tracking fields.
/// </summary>
/// <remarks>
/// <para>
/// This version was introduced for GDPR compliance (Article 7).
/// All data processing requires demonstrable consent.
/// </para>
/// <para>
/// Legacy profiles (V1/V2) are considered "grandfathered" - they were
/// created before consent requirements and are assumed to have consent
/// under the legitimate interest basis.
/// </para>
/// </remarks>
public sealed record UserProfileUpdatedEventV3 : IDispatchMessage, IVersionedMessage
{
	/// <summary>User's unique identifier.</summary>
	public Guid UserId { get; init; }

	/// <summary>User's first/given name.</summary>
	public string FirstName { get; init; } = string.Empty;

	/// <summary>User's last/family name.</summary>
	public string LastName { get; init; } = string.Empty;

	/// <summary>Whether user has given consent for data processing.</summary>
	public bool ConsentGiven { get; init; }

	/// <summary>When consent was recorded (null for legacy/grandfathered users).</summary>
	public DateTimeOffset? ConsentDate { get; init; }

	/// <inheritdoc/>
	public int Version => 3;

	/// <inheritdoc/>
	public string MessageType => "UserProfileUpdatedEvent";
}

/// <summary>
/// V4: Added encrypted email support for privacy enhancement.
/// </summary>
/// <remarks>
/// <para>
/// This version supports field-level encryption for sensitive PII.
/// The Email field may contain encrypted data depending on IsEmailEncrypted flag.
/// </para>
/// <para>
/// Legacy profiles are upcasted with empty Email (user must re-verify email)
/// and IsEmailEncrypted=false.
/// </para>
/// </remarks>
public sealed record UserProfileUpdatedEventV4 : IDispatchMessage, IVersionedMessage
{
	/// <summary>User's unique identifier.</summary>
	public Guid UserId { get; init; }

	/// <summary>User's first/given name.</summary>
	public string FirstName { get; init; } = string.Empty;

	/// <summary>User's last/family name.</summary>
	public string LastName { get; init; } = string.Empty;

	/// <summary>Whether user has given consent for data processing.</summary>
	public bool ConsentGiven { get; init; }

	/// <summary>When consent was recorded (null for legacy/grandfathered users).</summary>
	public DateTimeOffset? ConsentDate { get; init; }

	/// <summary>User's email address (may be encrypted if IsEmailEncrypted is true).</summary>
	public string Email { get; init; } = string.Empty;

	/// <summary>Indicates whether the Email field contains encrypted data.</summary>
	public bool IsEmailEncrypted { get; init; }

	/// <inheritdoc/>
	public int Version => 4;

	/// <inheritdoc/>
	public string MessageType => "UserProfileUpdatedEvent";
}
