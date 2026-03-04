// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for CDC configuration.
/// </summary>
public static class CdcExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures CDC processing for the Excalibur host.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">The CDC configuration action.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder AddCdc(
		this IExcaliburBuilder builder,
		Action<ICdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddCdcProcessor(configure);
		return builder;
	}
}
