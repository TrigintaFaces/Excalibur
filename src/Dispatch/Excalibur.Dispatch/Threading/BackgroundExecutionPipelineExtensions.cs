// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Extension methods for adding background execution middleware to the dispatch pipeline.
/// </summary>
public static class BackgroundExecutionPipelineExtensions
{
	/// <summary>
	/// Adds background execution middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The background execution middleware offloads handler execution to a background
	/// thread, allowing the caller to return immediately while processing continues
	/// asynchronously.
	/// </para>
	/// <para>
	/// This method also registers the hosted service that awaits in-flight background work when the host
	/// stops gracefully, bounded by <c>HostOptions.ShutdownTimeout</c>. Work still running when that budget
	/// elapses is signalled to cancel, and is lost and logged as an error: background execution is
	/// in-process and best-effort, not durable. Work that must survive a crash or restart belongs in the outbox.
	/// </para>
	/// <para>
	/// What happens when a background message fails is set by
	/// <see cref="Options.Threading.BackgroundExecutionOptions.ExceptionBehavior"/>, configured with
	/// <c>services.Configure&lt;BackgroundExecutionOptions&gt;(...)</c>.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseBackgroundExecution()  // Offload after validation
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseBackgroundExecution(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Enabling background execution enables its shutdown drain and its validated failure policy. The
		// middleware is the documented way in, and a consumer who follows the documentation must not have
		// in-flight messages abandoned at shutdown because the drain lived only behind a second registration.
		_ = builder.Services.AddBackgroundExecutionServices();

		return builder.UseMiddleware<BackgroundExecutionMiddleware>();
	}
}
