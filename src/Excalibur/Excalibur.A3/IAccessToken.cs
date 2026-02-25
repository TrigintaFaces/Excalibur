// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;

namespace Excalibur.A3;

/// <summary>
/// Interface that combines authentication and authorization capabilities for accessing secured resources.
/// </summary>
public interface IAccessToken : IAuthenticationToken, IAuthorizationPolicy
{
	/// <summary>
	/// Gets the user identifier associated with the access token.
	/// </summary>
	/// <value>The user identifier as a string.</value>
	/// <remarks>
	/// This property overrides the <see cref="IAuthenticationToken.UserId" /> to provide a unified user identifier in contexts
	/// requiring both authentication and authorization.
	/// </remarks>
	new string UserId { get; }
}
