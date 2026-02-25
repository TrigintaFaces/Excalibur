// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Specifies which features a middleware component requires. Implements requirement R2.6. Pipeline synthesizer will omit middleware
/// requiring disabled features.
/// </summary>
/// <param name="features"> The features this middleware requires. </param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresFeaturesAttribute(params DispatchFeatures[] features) : Attribute
{
	/// <summary>
	/// Gets the features this middleware requires.
	/// </summary>
	/// <value> The list of features that must be enabled to use the middleware. </value>
	public IReadOnlyList<DispatchFeatures> Features { get; } = features ?? [];
}
