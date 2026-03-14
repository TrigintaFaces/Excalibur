// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Versioning;

/// <summary>
/// Extension methods for adding contract versioning middleware to the dispatch pipeline.
/// </summary>
public static class ContractVersioningPipelineExtensions
{
	/// <summary>
	/// Adds contract version checking middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The contract versioning middleware validates message contract versions,
	/// ensuring that handlers receive messages compatible with their expected schema.
	/// </para>
	/// <para>
	/// Contract versioning services must be registered separately in the DI container.
	/// This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseContractVersioning()   // Validate version before handler
	///        .UseValidation()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseContractVersioning(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<ContractVersionCheckMiddleware>();
	}
}
