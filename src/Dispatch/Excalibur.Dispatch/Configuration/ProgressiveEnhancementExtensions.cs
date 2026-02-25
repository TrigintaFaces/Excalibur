// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Progressive enhancement extension methods for <see cref="IDispatchBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a layered approach to adding Dispatch features.
/// Start minimal and progressively add capabilities as needed:
/// </para>
/// <code>
/// // Minimal (MediatR replacement)
/// services.AddDispatch();
///
/// // With specific features
/// services.AddDispatch()
///     .AddContextEnrichment()
///     .AddDispatchValidation();
///
/// // Full-featured mode
/// services.AddDispatch()
///     .AddAllFeatures();
/// </code>
/// <para>
/// Note: Inbox/Outbox require explicit store configuration and are not included
/// in <see cref="AddAllFeatures"/>. Use <c>WithInbox&lt;TStore&gt;()</c> and
/// <c>WithOutbox&lt;TStore&gt;()</c> separately.
/// </para>
/// </remarks>
public static class ProgressiveEnhancementExtensions
{
	/// <summary>
	/// Adds context enrichment capabilities to the dispatch pipeline.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Context enrichment enables full message context tracking including:
	/// </para>
	/// <list type="bullet">
	///   <item>Full <see cref="Excalibur.Dispatch.Abstractions.IMessageContext"/> with Items dictionary</item>
	///   <item>Correlation ID propagation across message flows</item>
	///   <item>AsyncLocal context flow via <see cref="IMessageContextAccessor"/></item>
	/// </list>
	/// <para>
	/// This is distinct from <c>AddContextObservability()</c> which focuses on telemetry
	/// (OpenTelemetry spans, metrics, and logging). Use both for complete context support.
	/// </para>
	/// </remarks>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static IDispatchBuilder AddContextEnrichment(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Enable full context mode (not lightweight)
		_ = builder.Services.Configure<DispatchOptions>(options =>
		{
			options.UseLightMode = false;
			options.Features.EnableCorrelation = true;
		});

		// Ensure AsyncLocal context accessor is registered
		builder.Services.TryAddSingleton<IMessageContextAccessor, MessageContextAccessor>();

		return builder;
	}

	/// <summary>
	/// Adds all standard Dispatch features available in the core package.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a convenience method that combines the following core extensions:
	/// </para>
	/// <list type="bullet">
	///   <item><see cref="AddContextEnrichment"/> - Full context with correlation IDs</item>
	///   <item><c>AddDispatchValidation()</c> - Message validation middleware</item>
	/// </list>
	/// <para>
	/// <strong>Note:</strong> The following require separate packages and should be added explicitly:
	/// </para>
	/// <list type="bullet">
	///   <item><c>AddContextObservability()</c> - Requires Excalibur.Dispatch.Observability package</item>
	///   <item><c>AddDispatchResilience()</c> - Requires Excalibur.Dispatch.Resilience.Polly package</item>
	///   <item><c>WithInbox&lt;TStore&gt;()</c> / <c>WithOutbox&lt;TStore&gt;()</c> - Require store provider choice</item>
	/// </list>
	/// <para>
	/// Full-featured example with all packages:
	/// </para>
	/// <code>
	/// services.AddDispatch()
	///     .AddAllFeatures()
	///     .AddContextObservability()                // From Excalibur.Dispatch.Observability
	///     .AddDispatchResilience()                  // From Excalibur.Dispatch.Resilience.Polly
	///     .WithInbox&lt;InMemoryInboxStore&gt;()
	///     .WithOutbox&lt;SqlServerOutboxStore&gt;();
	/// </code>
	/// </remarks>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static IDispatchBuilder AddAllFeatures(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder
			.AddContextEnrichment()       // Context flow with correlation IDs
			.AddDispatchValidation();     // Message validation
	}
}
