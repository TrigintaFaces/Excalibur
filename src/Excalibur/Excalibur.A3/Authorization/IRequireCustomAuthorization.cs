// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.AspNetCore.Authorization;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Interface for messages that require custom authorization using specific authorization requirements.
/// </summary>
public interface IRequireCustomAuthorization : IRequireAuthorization
{
	/// <summary>
	/// Gets the collection of custom authorization requirements that must be satisfied.
	/// </summary>
	/// <value>The collection of custom authorization requirements.</value>
	IEnumerable<IAuthorizationRequirement> AuthorizationRequirements { get; }
}
