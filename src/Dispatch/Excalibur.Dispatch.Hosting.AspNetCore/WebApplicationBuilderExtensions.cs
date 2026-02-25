// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

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
	/// <param name="configure">Optional configuration action for the dispatch builder.</param>
	/// <returns>The web application builder for chaining.</returns>
	public static WebApplicationBuilder AddDispatch(this WebApplicationBuilder builder, Action<IDispatchBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatch(configure);
		return builder;
	}
}
