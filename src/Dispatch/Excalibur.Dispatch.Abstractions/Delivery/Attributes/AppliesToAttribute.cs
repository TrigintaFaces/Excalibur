// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Indicates which message kinds a middleware component applies to.
/// </summary>
/// <remarks>
/// This attribute allows declarative configuration of middleware applicability based on message kinds. When applied to a middleware class,
/// it overrides the ApplicableMessageKinds property of IDispatchMiddleware. Multiple message kinds can be specified using the flags enum pattern.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="AppliesToAttribute" /> class. </remarks>
/// <param name="messageKinds"> The message kinds this middleware applies to. </param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AppliesToAttribute(MessageKinds messageKinds) : Attribute
{
	/// <summary>
	/// Gets the message kinds this middleware applies to.
	/// </summary>
	/// <value> The message kinds supported by the middleware. </value>
	public MessageKinds MessageKinds { get; } = messageKinds;
}
