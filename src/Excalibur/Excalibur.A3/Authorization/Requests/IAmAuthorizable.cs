// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Represents an interface for objects that require authorization.
/// </summary>
/// <remarks>
/// Objects implementing this interface use an <see cref="IAccessToken" /> to perform authorization checks. The token contains identity
/// and claims information for determining access rights.
/// </remarks>
public interface IAmAuthorizable : IRequireAuthorization
{
	/// <summary>
	/// Gets or sets the access token used to authorize the object.
	/// </summary>
	/// <value> The access token containing user identity and claims information for authorization. </value>
	IAccessToken? AccessToken { get; set; }
}
