// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Logging;

/// <summary>
/// Extension methods for adding audit logging middleware to the dispatch pipeline.
/// </summary>
public static class AuditLoggingPipelineExtensions
{
	/// <summary>
	/// Adds audit logging middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The audit logging middleware records dispatch operations for compliance and
	/// auditing purposes, capturing who dispatched what and when.
	/// </para>
	/// <para>
	/// Audit logging services must be registered separately in the DI container. This method
	/// only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseAuditLogging()         // Log after auth for actor context
	///        .UseValidation()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseAuditLogging(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<AuditLoggingMiddleware>();
	}
}
