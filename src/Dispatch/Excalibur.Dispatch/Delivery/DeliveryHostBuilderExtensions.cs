// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring Dispatch using <see cref="IHostBuilder" />.
/// </summary>
public static class DeliveryHostBuilderExtensions
{
	/// <summary>
	/// Adds Dispatch to the host builder with optional configuration.
	/// </summary>
	/// <param name="builder">The host builder.</param>
	/// <param name="configure">Optional configuration action for the dispatch builder.</param>
	/// <returns>The host builder for chaining.</returns>
	public static IHostBuilder AddDispatch(this IHostBuilder builder, Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.ConfigureServices((_, services) => services.AddDispatch(configure));
		return builder;
	}
}
