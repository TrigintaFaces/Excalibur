// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Audit.Events;

/// <summary>
/// Represents the identity portion of an audited activity, capturing who performed the action.
/// </summary>
public interface IAuditIdentity
{
	/// <summary>
	/// Gets the name of the activity being audited.
	/// </summary>
	/// <value>The name of the activity being audited.</value>
	string ActivityName { get; init; }

	/// <summary>
	/// Gets the name of the application where the activity occurred.
	/// </summary>
	/// <value>The name of the application where the activity occurred.</value>
	string ApplicationName { get; init; }

	/// <summary>
	/// Gets the user identifier of the individual performing the activity.
	/// </summary>
	/// <value>The user identifier of the individual performing the activity.</value>
	string UserId { get; init; }

	/// <summary>
	/// Gets the name of the user performing the activity.
	/// </summary>
	/// <value>The name of the user performing the activity.</value>
	string UserName { get; init; }
}
