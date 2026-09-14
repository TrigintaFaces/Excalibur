// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Utility class for representing and managing unique keys associated with grants in a system. A grant key is composed of a userId and
/// a GrantScope, where the GrantScope encapsulates the tenantId, grantType, and qualifier. This class ensures consistent construction,
/// parsing, and serialization of grant keys.
/// </summary>
public sealed class GrantKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GrantKey" /> class.
	/// </summary>
	/// <param name="userId"> The user ID associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID for the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier for the grant. </param>
	/// <exception cref="ArgumentException"> Thrown if any argument is null or empty. </exception>
	public GrantKey(string? userId, string? tenantId, string? grantType, string? qualifier)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);
		ArgumentException.ThrowIfNullOrEmpty(tenantId);
		ArgumentException.ThrowIfNullOrEmpty(grantType);
		ArgumentException.ThrowIfNullOrEmpty(qualifier);

		UserId = userId;
		Scope = new GrantScope(tenantId, grantType, qualifier);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GrantKey" /> class from a serialized key string.
	/// </summary>
	/// <param name="key"> The serialized grant key in the format {userId}:{tenant}:{type}:{qualifier}. </param>
	/// <exception cref="ArgumentException"> Thrown if the key is null, empty, or improperly formatted. </exception>
	public GrantKey(string key)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);

		// RemoveEmptyEntries is deliberately absent: it SHIFTS the remaining segments left, so an empty
		// user id would promote the tenant into the user position and the key would address a different
		// grant. An empty segment must fail the length check, not be silently dropped.
		//
		// The count-limited split is safe only because the segments are escaped: no segment can contain a
		// raw ':', so exactly three separators are present and the fourth part cannot absorb one.
		var parts = key.Split(':', 4);

		if (parts.Length != 4)
		{
			throw new ArgumentException(
				$"The {nameof(key)} argument is invalid. The expected format is: {{userId}}:{{tenant}}:{{type}}:{{qualifier}}");
		}

		UserId = GrantKeyFormat.Unescape(parts[0]);
		Scope = new GrantScope(
			GrantKeyFormat.Unescape(parts[1]),
			GrantKeyFormat.Unescape(parts[2]),
			GrantKeyFormat.Unescape(parts[3]));
	}

	/// <summary>
	/// Gets the user ID associated with the grant.
	/// </summary>
	/// <value>The user ID.</value>
	public string UserId { get; init; }

	/// <summary>
	/// Gets the scope of the grant, which includes tenant, type, and qualifier.
	/// </summary>
	/// <value>The scope of the grant.</value>
	public GrantScope Scope { get; init; }

	/// <summary>
	/// Converts the <see cref="GrantKey" /> instance to its string representation.
	/// </summary>
	/// <returns> A string representation of the grant key in the format {userId}:{tenant}:{type}:{qualifier}. </returns>
	// Scope escapes its own three segments, so only the user id is escaped here.
	public override string ToString() => $"{GrantKeyFormat.Escape(UserId)}:{Scope}";
}
