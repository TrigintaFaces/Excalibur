// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for creating pipeline profiles.
/// </summary>
public interface IPipelineProfileBuilder
{
	/// <summary>
	/// Sets the message kinds this profile applies to.
	/// </summary>
	/// <param name="messageKinds"> The message kinds to support. </param>
	/// <returns> The builder for chaining. </returns>
	IPipelineProfileBuilder ForMessageKinds(MessageKinds messageKinds);

	/// <summary>
	/// Adds a middleware type to the profile.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <returns> The builder for chaining. </returns>
	IPipelineProfileBuilder UseMiddleware<TMiddleware>()
		where TMiddleware : IDispatchMiddleware;

	/// <summary>
	/// Builds the pipeline profile.
	/// </summary>
	/// <returns> The configured pipeline profile. </returns>
	IPipelineProfile Build();
}
