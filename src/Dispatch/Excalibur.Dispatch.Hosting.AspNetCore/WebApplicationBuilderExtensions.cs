// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for configuring Dispatch with <see cref="WebApplicationBuilder" />.
/// </summary>
public static class WebApplicationBuilderExtensions
{
	/// <summary>
	/// Adds Dispatch to the web application builder with optional configuration.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <param name="configure">Optional configuration action for the dispatch builder. Supplying it takes over handler registration: when it is omitted, handlers are discovered by scanning the entry assembly; when it is supplied, only the handlers it names are registered.</param>
	/// <returns>The web application builder for chaining.</returns>
	/// <remarks>
	/// Registers a startup validator that runs when the application is built. A configuration that cannot
	/// work — no dispatcher registered, or the outbox enabled with no outbox store — fails the build rather
	/// than starting a host that would drop messages silently. The exception message names the registration
	/// call that resolves it.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static WebApplicationBuilder AddDispatch(this WebApplicationBuilder builder, Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// The caller who wrote no lambda asked for the zero-config composition, and the framework must not
		// turn that into "configured, with nothing in it" by synthesising one. Name the discovery instead:
		// one predicate here rather than two shapes, so the guard that stops this regressing has a single
		// thing to look for. The configure overload registers exactly what its lambda names and scans
		// nothing, which is what keeps it usable from a trimmed application.
		_ = builder.Services.AddDispatch(configure ?? (static d => d.AddHandlersFromEntryAssembly()));

		// Flow the active request scope to scoped message handlers (so they share request-scoped state
		// instead of resolving from a fresh scope). Safe and idempotent via TryAdd.
		_ = builder.Services.AddDispatchAmbientScope();

		// Register startup filter for early validation during Build()
		builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, DispatchStartupFilter>());

		return builder;
	}
}
