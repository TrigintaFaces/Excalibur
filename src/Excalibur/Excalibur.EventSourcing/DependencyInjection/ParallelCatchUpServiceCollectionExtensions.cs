// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.ParallelCatchUp;
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
}
