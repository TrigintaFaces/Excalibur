// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under multiple licenses - see LICENSE files in project root.

using Excalibur.Dispatch.Abstractions;

using UserProfileVersioning.Events;

namespace UserProfileVersioning.Upcasters;

/// <summary>
/// Upcasts UserProfileUpdatedEventV1 to V2: Splits Name into FirstName/LastName.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b> Split on first space character.
/// </para>
/// <list type="bullet">
///   <item>"John Doe" → FirstName="John", LastName="Doe"</item>
///   <item>"Madonna" → FirstName="Madonna", LastName=""</item>
///   <item>"Mary Jane Watson" → FirstName="Mary", LastName="Jane Watson"</item>
/// </list>
/// </remarks>
public sealed class UserProfileV1ToV2Upcaster : IMessageUpcaster<UserProfileUpdatedEventV1, UserProfileUpdatedEventV2>
{
	/// <inheritdoc/>
	public int FromVersion => 1;

	/// <inheritdoc/>
	public int ToVersion => 2;

	/// <inheritdoc/>
	public UserProfileUpdatedEventV2 Upcast(UserProfileUpdatedEventV1 oldMessage)
	{
		var nameParts = oldMessage.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

		return new UserProfileUpdatedEventV2
		{
			UserId = oldMessage.UserId,
			FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
			LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty
		};
	}
}

/// <summary>
/// Upcasts UserProfileUpdatedEventV2 to V3: Adds GDPR consent fields.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b> Legacy profiles are "grandfathered" with implied consent.
/// </para>
/// <list type="bullet">
///   <item>ConsentGiven = true (assumed under legitimate interest basis)</item>
///   <item>ConsentDate = null (consent was implicit, no explicit date recorded)</item>
/// </list>
/// <para>
/// <b>Legal basis:</b> GDPR Article 6(1)(f) - Legitimate Interests.
/// Pre-GDPR accounts are assumed to have consent under the transitional provisions.
/// </para>
/// </remarks>
public sealed class UserProfileV2ToV3Upcaster : IMessageUpcaster<UserProfileUpdatedEventV2, UserProfileUpdatedEventV3>
{
	/// <inheritdoc/>
	public int FromVersion => 2;

	/// <inheritdoc/>
	public int ToVersion => 3;

	/// <inheritdoc/>
	public UserProfileUpdatedEventV3 Upcast(UserProfileUpdatedEventV2 oldMessage)
	{
		return new UserProfileUpdatedEventV3
		{
			UserId = oldMessage.UserId,
			FirstName = oldMessage.FirstName,
			LastName = oldMessage.LastName,
			ConsentGiven = true,   // Grandfathered: implied consent
			ConsentDate = null     // No explicit consent date for legacy users
		};
	}
}

/// <summary>
/// Upcasts UserProfileUpdatedEventV3 to V4: Adds encrypted email support.
/// </summary>
/// <remarks>
/// <para>
/// <b>Migration Strategy:</b> Legacy profiles have no email on file.
/// </para>
/// <list type="bullet">
///   <item>Email = "" (user must re-verify their email)</item>
///   <item>IsEmailEncrypted = false (no encryption applied to empty email)</item>
/// </list>
/// <para>
/// <b>Security consideration:</b> We don't try to "invent" email addresses
/// for legacy users. They must explicitly provide and verify their email
/// after this schema migration.
/// </para>
/// </remarks>
public sealed class UserProfileV3ToV4Upcaster : IMessageUpcaster<UserProfileUpdatedEventV3, UserProfileUpdatedEventV4>
{
	/// <inheritdoc/>
	public int FromVersion => 3;

	/// <inheritdoc/>
	public int ToVersion => 4;

	/// <inheritdoc/>
	public UserProfileUpdatedEventV4 Upcast(UserProfileUpdatedEventV3 oldMessage)
	{
		return new UserProfileUpdatedEventV4
		{
			UserId = oldMessage.UserId,
			FirstName = oldMessage.FirstName,
			LastName = oldMessage.LastName,
			ConsentGiven = oldMessage.ConsentGiven,
			ConsentDate = oldMessage.ConsentDate,
			Email = string.Empty,        // User must re-verify email
			IsEmailEncrypted = false     // No encryption on empty email
		};
	}
}
