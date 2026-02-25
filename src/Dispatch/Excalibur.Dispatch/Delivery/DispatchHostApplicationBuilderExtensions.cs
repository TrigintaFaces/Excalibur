// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring Dispatch services in host application builders.
/// </summary>
public static class DispatchHostApplicationBuilderExtensions
{
	/// <summary>
	/// Adds Dispatch to the host application builder with optional configuration.
	/// </summary>
	/// <param name="builder">The host application builder.</param>
	/// <param name="configure">Optional configuration action for the dispatch builder.</param>
	/// <returns>The host application builder for chaining.</returns>
	public static IHostApplicationBuilder AddDispatch(this IHostApplicationBuilder builder, Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatch(configure);
		return builder;
	}
}
