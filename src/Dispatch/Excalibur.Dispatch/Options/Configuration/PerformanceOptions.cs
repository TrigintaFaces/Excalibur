// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Controls how much context state Dispatch initializes on the direct local fast path.
/// </summary>
public enum DirectLocalContextInitializationProfile
{
	/// <summary>
	/// Initialize only required hot-path fields (message reference).
	/// Correlation/causation/message type are populated only when already present.
	/// </summary>
	Lean = 0,

	/// <summary>
	/// Initialize the full context surface used by the classic dispatch path.
	/// </summary>
	Full = 1,
}

/// <summary>
/// Configuration options for performance optimizations.
/// </summary>
public sealed class PerformanceOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether Cache stage middleware is included in synthesized pipelines.
	/// </summary>
	/// <remarks>
	/// When <c>true</c> (default), middleware registered in the
	/// <see cref="Abstractions.Middleware.DispatchMiddlewareStage.Cache"/> stage will be
	/// included in automatically synthesized pipelines.
	/// </remarks>
	/// <value><see langword="true"/> to include cache middleware; otherwise, <see langword="false"/>.</value>
	public bool EnableCacheMiddleware { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable type metadata caching.
	/// </summary>
	/// <value>The current <see cref="EnableTypeMetadataCaching"/> value.</value>
	public bool EnableTypeMetadataCaching { get; set; } = true;

	/// <summary>
	/// Gets or sets the size of the message pool for object reuse.
	/// </summary>
	/// <value>The current <see cref="MessagePoolSize"/> value.</value>
	public int MessagePoolSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to use allocation-free middleware execution.
	/// </summary>
	/// <value>The current <see cref="UseAllocationFreeExecution"/> value.</value>
	public bool UseAllocationFreeExecution { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically freeze caches when the application starts.
	/// </summary>
	/// <remarks>
	/// <para>
	/// PERF-22: When enabled (default), Dispatch will automatically freeze all internal caches
	/// when <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime.ApplicationStarted"/> fires.
	/// This provides optimal production performance with zero configuration.
	/// </para>
	/// <para>
	/// Auto-freeze is automatically disabled when hot reload is detected via the
	/// <c>DOTNET_WATCH</c> or <c>DOTNET_MODIFIABLE_ASSEMBLIES</c> environment variables.
	/// This ensures handler discovery works correctly during development.
	/// </para>
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to automatically freeze caches on startup; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="true"/>.
	/// </value>
	public bool AutoFreezeOnStart { get; set; } = true;

	/// <summary>
	/// Gets or sets the direct-local context initialization profile.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="DirectLocalContextInitializationProfile.Lean"/> minimizes local dispatch overhead and is optimized
	/// for MediatR-style in-process replacement scenarios.
	/// </para>
	/// <para>
	/// Set to <see cref="DirectLocalContextInitializationProfile.Full"/> to force legacy eager context initialization.
	/// </para>
	/// </remarks>
	public DirectLocalContextInitializationProfile DirectLocalContextInitialization { get; set; } =
		DirectLocalContextInitializationProfile.Lean;

	/// <summary>
	/// Gets or sets a value indicating whether direct-local success results should include full metadata.
	/// </summary>
	/// <remarks>
	/// When <see langword="false"/> (default), direct-local success uses minimal result objects for hot-path performance.
	/// </remarks>
	public bool EmitDirectLocalResultMetadata { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to automatically promote stateless handlers to singleton lifetime.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When enabled, Dispatch inspects handler constructor dependencies at registration time.
	/// Handlers whose dependencies are all singletons (or that have no dependencies) are automatically
	/// promoted from transient to singleton registration, eliminating per-dispatch DI resolution
	/// and handler allocation.
	/// </para>
	/// <para>
	/// This follows the ASP.NET Core minimal API pattern where stateless delegates are effectively singleton.
	/// Handlers with scoped or transient dependencies (e.g., DbContext) are NOT promoted.
	/// </para>
	/// </remarks>
	/// <value>
	/// <see langword="false"/> by default for safety. Set to <see langword="true"/> to enable auto-promotion.
	/// </value>
	public bool AutoPromoteStatelessHandlersToSingleton { get; set; }
}
