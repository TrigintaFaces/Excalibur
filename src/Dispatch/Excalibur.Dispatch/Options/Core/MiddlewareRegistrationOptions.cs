// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for middleware registrations.
/// </summary>
public sealed class MiddlewareRegistrationOptions
{
	/// <summary>
	/// Gets the collection of middleware registrations.
	/// </summary>
	/// <value> The list of middleware registrations applied to the pipeline. </value>
	public List<MiddlewareRegistration> Registrations { get; } = [];
}
