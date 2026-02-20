// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Interface for middleware that can have its stage configured.
/// </summary>
internal interface IConfigurableMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// Gets or sets the stage for this middleware.
	/// </summary>
	/// <value>
	/// The stage for this middleware.
	/// </value>
	new DispatchMiddlewareStage Stage { get; set; }
}
