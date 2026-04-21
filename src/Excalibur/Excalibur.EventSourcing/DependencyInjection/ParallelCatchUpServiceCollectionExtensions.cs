// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.ParallelCatchUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring parallel global stream catch-up processing.
/// </summary>
public static class ParallelCatchUpServiceCollectionExtensions
{
	/// <summary>
	/// Enables parallel catch-up processing for the global stream projection host.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure parallel catch-up options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IEventSourcingBuilder UseParallelCatchUp(
		this IEventSourcingBuilder builder,
		Action<ParallelCatchUpOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ParallelCatchUpOptions>, ParallelCatchUpOptionsValidator>());
		builder.Services.AddOptionsWithValidateOnStart<ParallelCatchUpOptions>();

		return builder;
	}

	/// <summary>
	/// Enables parallel catch-up processing for the global stream projection host,
	/// with options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="ParallelCatchUpOptions"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload binds options from configuration (e.g., <c>appsettings.json</c>) instead of
	/// an imperative <see cref="Action{T}"/> delegate. Data annotations are validated on start.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IEventSourcingBuilder UseParallelCatchUp(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<ParallelCatchUpOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ParallelCatchUpOptions>, ParallelCatchUpOptionsValidator>());

		return builder;
	}
}
