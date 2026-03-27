// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Marker interface for commands that create or modify grants.
/// </summary>
/// <remarks>
/// <para>
/// Enables middleware (e.g., SoD preventive enforcement) to detect grant-related
/// commands without reflection or hard-coded type-name matching.
/// </para>
/// </remarks>
public interface IGrantCommand
{
	/// <summary>
	/// Gets the identifier of the user receiving the grant.
	/// </summary>
	/// <value>The user/subject identifier.</value>
	string UserId { get; }

	/// <summary>
	/// Gets the qualifier (scope) of the grant being created or modified.
	/// </summary>
	/// <value>The grant qualifier.</value>
	string Qualifier { get; }

	/// <summary>
	/// Gets the type of grant being created or modified.
	/// </summary>
	/// <value>The grant type (e.g., Activity, ActivityGroup, Role).</value>
	string GrantType { get; }
}
