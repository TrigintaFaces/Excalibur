// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Indicates which message kinds a middleware component should exclude from processing.
/// </summary>
/// <remarks>
/// This attribute allows declarative configuration of middleware exclusions based on message kinds. When applied to a middleware class, it
/// specifies which message kinds should be excluded from processing, even if they would otherwise be included by the ApplicableMessageKinds
/// property. This is useful for creating middleware that handles most message types except specific ones.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="ExcludeKindsAttribute" /> class. </remarks>
/// <param name="messageKinds"> The message kinds to exclude from processing. </param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ExcludeKindsAttribute(MessageKinds messageKinds) : Attribute
{
	/// <summary>
	/// Gets the message kinds to exclude from processing.
	/// </summary>
	/// <value> The message kinds that should be skipped by the middleware. </value>
	public MessageKinds ExcludedKinds { get; } = messageKinds;
}
