// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.PipelineDiagnostics;

/// <summary>
/// Extension methods for adding diagnostic middleware to the dispatch pipeline.
/// </summary>
public static class DiagnosticPipelineExtensions
{
	/// <summary>
	/// Adds diagnostic middleware to the dispatch pipeline that logs pipeline state,
	/// middleware registrations, and message dispatch information at Debug level.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// The diagnostic middleware runs at <see cref="Abstractions.DispatchMiddlewareStage.Start"/>
	/// and logs pipeline composition on first invocation, plus message type and ID for every dispatch.
	/// All output is at <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/> level.
	/// </para>
	/// <para>
	/// Recommended for development environments only. Pipeline state is logged once on first dispatch
	/// to avoid noise.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseDiagnostics(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<DiagnosticMiddleware>();
	}
}
