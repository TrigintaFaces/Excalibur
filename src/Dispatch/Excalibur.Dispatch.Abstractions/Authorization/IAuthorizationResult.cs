// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the result of an authorization check for a message.
/// </summary>
/// <remarks>
/// Authorization results indicate whether the current user or context has permission to process a specific message. This is used by the
/// authorization middleware to enforce security policies before message handlers are invoked. Key features include:
/// <list type="bullet">
/// <item> Boolean authorization status indicator </item>
/// <item> Failure message for unauthorized access attempts </item>
/// <item> Integration with ASP.NET Core authorization policies </item>
/// <item> Support for role-based and claims-based authorization </item>
/// </list>
/// Implementations should provide meaningful failure messages that help with debugging while avoiding sensitive information disclosure in
/// production environments.
/// </remarks>
public interface IAuthorizationResult
{
	/// <summary>
	/// Gets the failure message when authorization is denied.
	/// </summary>
	/// <remarks>
	/// Contains a human-readable description of why authorization failed. Should be null when IsAuthorized is true. In production
	/// environments, consider limiting the detail in failure messages to prevent information disclosure that could aid attackers.
	/// </remarks>
	/// <value> The description explaining why authorization failed, or <see langword="null" /> when authorized. </value>
	string? FailureMessage { get; }

	/// <summary>
	/// Gets a value indicating whether the authorization check passed.
	/// </summary>
	/// <remarks>
	/// When true, the current security context has permission to process the message and processing can continue. When false, the message
	/// should be rejected and the FailureMessage should provide details about the denial.
	/// </remarks>
	/// <value> <see langword="true" /> when authorization succeeded; otherwise, <see langword="false" />. </value>
	bool IsAuthorized { get; }
}
